using System.IO.Pipelines;
using System.Net.WebSockets;
using System.Threading.Channels;
using KestrelSocket.Core;

namespace KestrelSocket.WebSocket
{
    internal class WebSocketPipeChannel<TPackage>(
        System.Net.WebSockets.WebSocket webSocket,
        IPackageDecoder<TPackage> packageDecoder,
        int maxPackageLength,
        WebSocketMessageType webSocketMessageType,
        string channelId) : IChannel<TPackage>
        where TPackage : PackageBase
    {
        private const int DefaultBufferSize = 65536;
        private readonly System.Net.WebSockets.WebSocket _webSocket = webSocket;
        private readonly IPackageDecoder<TPackage> _packageDecoder = packageDecoder;
        private readonly int _maxPackageLength = maxPackageLength;
        private readonly WebSocketMessageType _webSocketMessageType = webSocketMessageType;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly Pipe _pipe = new(new PipeOptions(
                pauseWriterThreshold: DefaultBufferSize,
                resumeWriterThreshold: DefaultBufferSize / 2,
                readerScheduler: PipeScheduler.ThreadPool,
                useSynchronizationContext: false));
        private readonly CancellationTokenSource _cts = new();

        public string ChannelId { get; private set; } = channelId;

        public bool IsClosed { get; private set; }

        public string? Endpoint { get; set; }

        public DateTimeOffset LastActiveTime { get; private set; } = DateTimeOffset.UtcNow;

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (this.IsClosed)
            {
                throw new InvalidOperationException("通道已关闭");
            }

            try
            {
                await this._sendLock.WaitAsync();

                if (this.IsClosed)
                {
                    throw new ChannelClosedException("通道已关闭");
                }

                // 发送消息给客户端
                await this._webSocket.SendAsync(data, this._webSocketMessageType, true, cancellationToken);
            }
            finally
            {
                this._sendLock.Release();
            }
        }

        /// <summary>
        /// 处理数据
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="PackageToLongException"></exception>
        public async Task<TPackage?> ReceivePacketAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var readTask = this._pipe.Reader.ReadAsync(cancellationToken);
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
                            break;
                        }
                    }
                    finally
                    {
                        this._pipe.Reader.AdvanceTo(consumed, examined);
                    }
                }
            }
            catch (Exception exception)
            {
                // 确保不会再读取数据
                this._pipe.Reader.Complete(exception);
                this._pipe.Writer.Complete(exception);
                throw;
            }

            cancellationToken.ThrowIfCancellationRequested();
            return default;
        }

        public ValueTask DisposeAsync()
        {
            this._cts.Cancel();
            this._pipe.Reader.Complete();
            this._pipe.Writer.Complete();
            this.IsClosed = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// 开始从WebSocket中读取数据
        /// </summary>
        /// <returns></returns>
        internal async Task StartReceivingAsync()
        {
            try
            {
                while (!this._cts.IsCancellationRequested)
                {
                    // Do a 0 byte read so that idle connections don't allocate a buffer when waiting for a read
                    var result = await this._webSocket.ReceiveAsync(Memory<byte>.Empty, this._cts.Token).ConfigureAwait(false);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    var memory = this._pipe.Writer.GetMemory();
                    var readTask = this._webSocket.ReceiveAsync(memory, this._cts.Token);
                    var receiveResult = readTask.IsCompleted ? readTask.Result : await readTask.ConfigureAwait(false);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        return;
                    }

                    this._pipe.Writer.Advance(receiveResult.Count);
                    var flushResult = await this._pipe.Writer.FlushAsync();

                    // We canceled in the middle of applying back pressure
                    // or if the consumer is done
                    if (flushResult.IsCanceled || flushResult.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
            {
                // Client has closed the WebSocket connection without completing the close handshake
            }
            catch (OperationCanceledException)
            {
                // Ignore aborts, don't treat them like transport errors
            }
            catch (Exception ex)
            {
                this._pipe.Writer.Complete(ex);
            }
            finally
            {
                // We're done writing
                this._pipe.Writer.Complete();
            }
        }
    }
}
