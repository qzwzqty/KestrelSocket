using System.IO.Pipelines;
using System.Net;
using System.Threading.Channels;
using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Packets;

namespace KestrelSocket.Mqtt
{
    internal class MqttPipeChannel(
        ConnectionContext connection,
        int maxPackageLength,
        ILoggerFactory loggerFactory) : IMqttChannel
    {
        private readonly ConnectionContext _connection = connection;
        private readonly int _maxPackageLength = maxPackageLength;
        private readonly PipeReader _input = connection.Transport.Input;
        private readonly PipeWriter _output = connection.Transport.Output;
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly MqttPacketFormatterAdapter _packetFormatterAdapter = new(new MqttBufferWriter(4096, 65535));
        private readonly ILogger _logger = loggerFactory.CreateLogger<MqttPipeChannel>();

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

        /// <summary>
        /// 接收MQTT包
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        /// <exception cref="PackageToLongException"></exception>
        public async Task<MqttPacket?> ReceivePacketAsync(CancellationToken cancellationToken)
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
                            if (this._packetFormatterAdapter.TryDecode(buffer, out var packet, out consumed, out examined, out var received))
                            {
                                return packet;
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
                    catch (MqttProtocolViolationException ex)
                    {
                        this._logger.LogWarning(ex, "解析MQTT消息出错");
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

        /// <summary>
        /// 发送MQTT包
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async ValueTask SendAsync(MqttPacket packet, CancellationToken cancellationToken)
        {
            if (this.IsClosed)
            {
                throw new ChannelClosedException("通道已关闭");
            }

            try
            {
                await this._sendLock.WaitAsync(cancellationToken);
                var buffer = this._packetFormatterAdapter.Encode(packet);

                if (buffer.Payload.Count == 0)
                {
                    // zero copy
                    // https://github.com/dotnet/runtime/blob/e31ddfdc4f574b26231233dc10c9a9c402f40590/src/libraries/System.IO.Pipelines/src/System/IO/Pipelines/StreamPipeWriter.cs#L279
                    await this._output.WriteAsync(buffer.Packet, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    WritePacketBuffer(this._output, buffer);
                    await this._output.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            finally
            {
                this._packetFormatterAdapter.Cleanup();
                this._sendLock.Release();
            }
        }

        public async ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default)
        {
            if (this.IsClosed)
            {
                throw new InvalidOperationException("通道已关闭");
            }

            try
            {
                await this._sendLock.WaitAsync(cancellationToken);

                if (this.IsClosed)
                {
                    throw new ChannelClosedException("通道已关闭");
                }

                // 发送消息给客户端
                await this._output.WriteAsync(data, cancellationToken);
                //await this._output.FlushAsync();
            }
            finally
            {
                this._sendLock.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            this._input.Complete();
            this._output.Complete();
            this.IsClosed = true;
            return ValueTask.CompletedTask;
        }

        private static void WritePacketBuffer(PipeWriter output, MqttPacketBuffer buffer)
        {
            // copy MqttPacketBuffer's Packet and Payload to the same buffer block of PipeWriter
            // MqttPacket will be transmitted within the bounds of a WebSocket frame after PipeWriter.FlushAsync

            var span = output.GetSpan(buffer.Length);

            buffer.Packet.AsSpan().CopyTo(span);
            buffer.Payload.AsSpan().CopyTo(span[buffer.Packet.Count..]);

            output.Advance(buffer.Length);
        }
    }
}
