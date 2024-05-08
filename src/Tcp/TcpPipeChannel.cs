using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;

namespace KestrelSocket.Tcp
{
    internal class TcpPipeChannel<TPackage>(
        ConnectionContext connection,
        IPackageDecoder<TPackage> packageDecoder,
        int maxPackageLength) : IChannel<TPackage>
        where TPackage : PackageBase
    {
        private readonly ConnectionContext _connection = connection;
        private readonly IPackageDecoder<TPackage> _packageDecoder = packageDecoder;
        private readonly int _maxPackageLength = maxPackageLength;
        private readonly PipeReader _input = connection.Transport.Input;
        private readonly PipeWriter _output = connection.Transport.Output;
        private readonly SemaphoreSlim _sendLock = new(1, 1);

        public string ChannelId { get; private set; } = connection.ConnectionId;

        public bool IsClosed { get; private set; }

        public string? Endpoint
        {
            get
            {
                // tcp
                if (this._connection.RemoteEndPoint != null)
                {
                    return this._connection.RemoteEndPoint.ToString();
                }

                // websocket
                var httpFeature = this._connection.Features.Get<IHttpConnectionFeature>();
                return httpFeature?.RemoteIpAddress != null
                    ? new IPEndPoint(httpFeature.RemoteIpAddress, httpFeature.RemotePort).ToString()
                    : null;
            }
        }

        public DateTimeOffset LastActiveTime { get; private set; } = DateTimeOffset.UtcNow;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (this.IsClosed)
            {
                throw new InvalidOperationException("通道已关闭");
            }

            try
            {
                await this._sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);

                if (this.IsClosed)
                {
                    throw new ChannelClosedException("通道已关闭");
                }

                // 发送消息给客户端
                await this._output.WriteAsync(data, cancellationToken).ConfigureAwait(false);
                //await this._output.FlushAsync();
            }
            finally
            {
                this._sendLock.Release();
            }
        }

        public async Task<TPackage?> ReceivePacketAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var readTask = this._input.ReadAsync(cancellationToken);
                    var readResult = readTask.IsCompleted ? readTask.Result : await readTask.ConfigureAwait(false);

                    var buffer = readResult.Buffer;
                    var consumed = buffer.Start;
                    var examined = buffer.End;

                    if (buffer.Length >= this._maxPackageLength)
                    {
                        throw new PackageTooLongException($"报文数据包太大，超过：{this._maxPackageLength}");
                    }

                    try
                    {
                        if (!buffer.IsEmpty)
                        {
                            // TODO：这样有个问题，不管报文有多长，每次都只能解析一次，剩下的要到下一次循环才能解析；应该尝试一次解析完成（粘包）。
                            this.LastActiveTime = DateTimeOffset.UtcNow;
                            if (this._packageDecoder.TryDecode(buffer, out var package, out consumed, out examined))
                            {
                                return package;
                            }

                            // 此时会继续读取
                        }

                        if (readResult.IsCompleted)
                        {
                            // 这里说明客户端断开连接了
                            //throw new MqttCommunicationException("Connection Aborted");
                            break;
                        }
                    }
                    finally
                    {
                        this._input.AdvanceTo(consumed, examined);
                    }
                }
            }
            catch (Exception exception)
            {
                // 确保不会再读取数据
                this._input.Complete(exception);
                this._output.Complete(exception);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }

        public ValueTask DisposeAsync()
        {
            this._input.Complete();
            this._output.Complete();
            this.IsClosed = true;
            return ValueTask.CompletedTask;
        }
    }
}
