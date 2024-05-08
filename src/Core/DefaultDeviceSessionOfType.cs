using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace KestrelSocket.Core
{
    /// <summary>
    /// Session
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public class DefaultDeviceSession<TPackage> : IDeviceSession
        where TPackage : PackageBase
    {
        private CancellationTokenSource? _cts;
        private readonly Channel<TPackage?> _packageChannel;
        private Task? _readTask;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly IChannel<TPackage> _channelOfType;
        private readonly ILogger _logger;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceKey">初次创建可以输入其他字符串，在解析完第一个报文的时候<see cref="ReceiveFirstPackageAsync"/>，会重新赋值</param>
        /// <param name="channel">带TPackage的通道</param>
        /// <param name="packageHandler"></param>
        /// <param name="loggerFactory"></param>
        public DefaultDeviceSession(
            string deviceKey,
            IChannel<TPackage> channel,
            IPackageHandler<TPackage> packageHandler,
            ILoggerFactory loggerFactory)
        {
            this.DeviceKey = deviceKey;
            this._channelOfType = channel;
            this.PackageHandler = packageHandler;
            this._logger = loggerFactory.CreateLogger("IotAdapterCommon.SocketCore.DefaultDeviceSession");
            this._packageChannel = System.Threading.Channels.Channel.CreateUnbounded<TPackage?>();
            this.Properties = new ConcurrentDictionary<string, IPropertyValue>();
        }

        /// <summary>
        /// 
        /// </summary>
        protected IPackageHandler<TPackage> PackageHandler { get; }

        /// <summary>
        /// 
        /// </summary>
        public ConcurrentDictionary<string, IPropertyValue> Properties { get; }

        /// <summary>
        /// 
        /// </summary>
        public IChannel Channel => this._channelOfType;

        /// <summary>
        /// 
        /// </summary>
        public string DeviceKey { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTimeOffset ConnectionTime { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public DateTimeOffset LastHeartbeatTime => this.Channel.LastActiveTime;

        /// <summary>
        /// 
        /// </summary>
        public bool SessionExpired { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public bool Started { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public bool Closed { get; private set; }

        /// <summary>
        /// 开始
        /// </summary>
        /// <returns>是否启动成功</returns>
        public virtual async Task<bool> StartAsync()
        {
            try
            {
                if (this.Started)
                {
                    return true;
                }

                await this._lock.WaitAsync().ConfigureAwait(false);

                if (this.Started)
                {
                    return true;
                }

                this._cts = new CancellationTokenSource();
                var cancellationToken = this._cts!.Token;

                // 等待第一条报文
                if (!await this.ReceiveFirstPackageAsync(cancellationToken).ConfigureAwait(false))
                {
                    return false;
                }

                // 开始读取后续报文
                this.ConnectionTime = DateTimeOffset.UtcNow;
                _ = Task.Factory.StartNew(async () => await this.StartHandlePackagesLoopAsync(cancellationToken).ConfigureAwait(false),
                    cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).ConfigureAwait(false);
                this._readTask = this.ReceivePackagesLoopAsync(cancellationToken);

                this.Started = true;
                this.Closed = false;
                return true;
            }
            finally
            {
                this._lock.Release();
            }
        }

        /// <summary>
        /// 关闭Session
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        public virtual Task CloseAsync(SessionCloseReasonType reason)
        {
            this.TryCancel();
            return Task.CompletedTask;
        }

        /// <summary>
        /// 解析第一个包
        /// </summary>
        /// <returns></returns>
        protected virtual async Task<bool> ReceiveFirstPackageAsync(CancellationToken cancellationToken)
        {
            // 两分钟内，还未读取到DeviceKey，则关闭连接
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            timeoutCts.Token.Register(this.TryCancel);

            try
            {
                // 读取一个包
                var firstPackage = await this._channelOfType.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
                if (firstPackage != null)
                {
                    this._packageChannel.Writer.TryWrite(firstPackage);
                    this.DeviceKey = firstPackage.DeviceKey;

                    return true;
                }
            }
            catch (Microsoft.AspNetCore.Connections.ConnectionResetException)
            {
                /*
                 * k8s TCP 健康检查是连上端口就断开，所以会报该错
                 * Failed to read from the pipe
                 * Microsoft.AspNetCore.Connections.ConnectionResetException: Connection reset by peer
                 *  --->System.Net.Sockets.SocketException(104): Connection reset by peer
                 *    at Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal.SocketConnection.DoReceive()
                 *    -- - End of inner exception stack trace-- -
                 *    at System.IO.Pipelines.Pipe.GetReadResult(ReadResult & result)
                 *    at System.IO.Pipelines.Pipe.ReadAsync(CancellationToken token)
                 *    at System.IO.Pipelines.Pipe.DefaultPipeReader.ReadAsync(CancellationToken cancellationToken)
                 *    at Iot.SuperSocket.Server.Kestrel.KestrelPipeChannel`1.ReceivePacketAsync()
                 */
            }
            catch (OperationCanceledException)
            {
            }

            return false;
        }

        /// <summary>
        /// 循环读取数据
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual async Task ReceivePackagesLoopAsync(CancellationToken cancellationToken)
        {
            TPackage? package;
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();

                    package = await this._channelOfType.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
                    if (package == null)
                    {
                        return;
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (this.SessionExpired)
                    {
                        return;
                    }

                    // 处理包
                    //await this._packageHandler.HandleAsync(this, package);
                    this._packageChannel.Writer.TryWrite(package);
                }
            }
            catch (Microsoft.AspNetCore.Connections.ConnectionResetException)
            {
                /*
                 * k8s TCP 健康检查是连上端口就断开，所以会报该错
                 * Failed to read from the pipe
                 * Microsoft.AspNetCore.Connections.ConnectionResetException: Connection reset by peer
                 *  --->System.Net.Sockets.SocketException(104): Connection reset by peer
                 *    at Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets.Internal.SocketConnection.DoReceive()
                 *    -- - End of inner exception stack trace-- -
                 *    at System.IO.Pipelines.Pipe.GetReadResult(ReadResult & result)
                 *    at System.IO.Pipelines.Pipe.ReadAsync(CancellationToken token)
                 *    at System.IO.Pipelines.Pipe.DefaultPipeReader.ReadAsync(CancellationToken cancellationToken)
                 *    at Iot.SuperSocket.Server.Kestrel.KestrelPipeChannel`1.ReceivePacketAsync()
                 */
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// 取消
        /// </summary>
        private void TryCancel()
        {
            try
            {
                this._cts?.Cancel(false);
            }
            catch (Exception)
            {
            }
            //catch (ObjectDisposedException)
            //{
            //}
            //catch (OperationCanceledException)
            //{
            //}
        }

        /// <summary>
        /// 循环处理Package
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task StartHandlePackagesLoopAsync(CancellationToken cancellationToken)
        {
            await Task.Yield();

            try
            {
                while (await this._packageChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (this._packageChannel.Reader.TryRead(out var package))
                    {
                        if (package != null)
                        {
                            try
                            {
                                await this.PackageHandler.HandleAsync(this, package);
                            }
                            catch (Exception ex)
                            {
                                this._logger.LogError(ex, "PackageHandler出错");
                            }
                        }
                    }
                }
            }
            finally
            {
            }
        }

        /// <summary>
        /// 处理关闭连接
        /// </summary>
        /// <returns></returns>
        private async ValueTask WaitHandleClosingAsync()
        {
            try
            {
                if (this._readTask != null)
                {
                    await this._readTask;
                    this._readTask = null;
                }

                this._packageChannel.Writer.Complete();
            }
            catch (OperationCanceledException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception)
            {
            }
            finally
            {
                this.TryCancel();
                this._cts?.Dispose();
                this._cts = null;

                this.Closed = true;
                this.Started = false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async ValueTask DisposeAsync()
        {
            await this.WaitHandleClosingAsync();
        }
    }
}
