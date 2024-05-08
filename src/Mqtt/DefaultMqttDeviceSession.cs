using System.Collections.Concurrent;
using System.Threading.Channels;
using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.Logging;
using MQTTnet.Client;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Packets;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace KestrelSocket.Mqtt
{
    internal class DefaultMqttDeviceSession(
        string deviceKey,
        IMqttChannel channel,
        ILoggerFactory loggerFactory,
        IPackageHandler<MqttPackage> packageHandler)
        : IDeviceSession
    {
        private CancellationTokenSource? _cts;
        private Task? _readTask;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly IMqttChannel _mqttChannel = channel;
        private readonly IPackageHandler<MqttPackage> _packageHandler = packageHandler;
        private readonly ILogger _logger = loggerFactory.CreateLogger("KestrelSocket.Mqtt.DefaultMqttDeviceSession");
        private readonly Channel<MqttPackage?> _packageChannel = System.Threading.Channels.Channel.CreateUnbounded<MqttPackage?>();

        public ConcurrentDictionary<string, IPropertyValue> Properties { get; } = new ConcurrentDictionary<string, IPropertyValue>();

        public IChannel Channel => this._mqttChannel;

        public string DeviceKey { get; private set; } = deviceKey;

        public DateTimeOffset ConnectionTime { get; private set; }

        public DateTimeOffset LastHeartbeatTime => this.Channel.LastActiveTime;

        public bool SessionExpired { get; set; }

        public bool Started { get; private set; }

        public bool Closed { get; private set; }

        public async Task<bool> StartAsync()
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
                var connectPacket = await this.ReceiveConnectPacketAsync(cancellationToken).ConfigureAwait(false);
                if (connectPacket == null)
                {
                    return false;
                }

                // 开始读取后续报文
                this.DeviceKey = connectPacket.ClientId;
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

        public Task CloseAsync(SessionCloseReasonType reason)
        {
            this.TryCancel();
            return Task.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            await this.WaitHandleClosingAsync();
        }

        #region 私有方法

        /// <summary>
        /// 读取第一个包：连接报文
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<MqttConnectPacket?> ReceiveConnectPacketAsync(CancellationToken cancellationToken)
        {
            // 两分钟内，还未读取到DeviceKey，则关闭连接
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            timeoutCts.Token.Register(this.TryCancel);

            try
            {
                // 读取一个包
                var firstPackage = await this._mqttChannel.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
                if (firstPackage is MqttConnectPacket connectPacket)
                {
                    this._logger.LogDebug("收到连接包：{pack}，ClientId：{clientId}", connectPacket.ToString(), connectPacket.ClientId);

                    // 回复设备消息
                    var hasClientId = !string.IsNullOrWhiteSpace(connectPacket.ClientId);

                    // 回复包
                    var connAckPacket = new MqttConnAckPacket
                    {
                        ReturnCode = hasClientId ? MqttConnectReturnCode.ConnectionAccepted : MqttConnectReturnCode.ConnectionRefusedNotAuthorized,
                        ReasonCode = hasClientId ? MqttConnectReasonCode.Success : MqttConnectReasonCode.ClientIdentifierNotValid,
                        RetainAvailable = true,
                        SubscriptionIdentifiersAvailable = true,
                        SharedSubscriptionAvailable = false,
                        TopicAliasMaximum = ushort.MaxValue,
                        MaximumQoS = MqttQualityOfServiceLevel.ExactlyOnce,
                        WildcardSubscriptionAvailable = true,
                        AuthenticationMethod = connectPacket.AuthenticationMethod,
                        AuthenticationData = connectPacket.AuthenticationData,
                        ReasonString = hasClientId ? null : "无ClientId",
                        ResponseInformation = null,
                        MaximumPacketSize = 0, // Unlimited,
                        ReceiveMaximum = 0 // Unlimited
                    };
                    await this._mqttChannel.SendAsync(connAckPacket, cancellationToken).ConfigureAwait(false);

                    // 如果不是成功，则断开连接
                    return connAckPacket.ReasonCode != MqttConnectReasonCode.Success
                        ? null
                        : connectPacket;
                }
            }
            catch (ConnectionResetException)
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

            return null;
        }

        /// <summary>
        /// 循环读取后续消息
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected virtual async Task ReceivePackagesLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Yield();

                    var currentPacket = await this._mqttChannel.ReceivePacketAsync(cancellationToken).ConfigureAwait(false);
                    if (currentPacket == null)
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
                    this._logger.LogDebug("收到MQTT包：{pack}", currentPacket.ToString());
                    switch (currentPacket)
                    {
                        case MqttPublishPacket publishPacket:

                            // 这是设备推送上来的数据
                            await this.HandleIncomingPublishPacketAsync(publishPacket, cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttPubAckPacket pubAckPacket:
                            break;

                        case MqttPubCompPacket pubCompPacket:
                            break;

                        case MqttPubRecPacket pubRecPacket:
                            await this.HandleIncomingPubRecPacketAsync(pubRecPacket, cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttPubRelPacket pubRelPacket:
                            await this.HandleIncomingPubRelPacketAsync(pubRelPacket, cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttSubscribePacket subscribePacket:
                            await this.HandleIncomingSubscribePacketAsync(subscribePacket, cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttUnsubscribePacket unsubscribePacket:
                            await this.HandleIncomingUnsubscribePacket(unsubscribePacket, cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttPingReqPacket:
                            await this.HandleIncomingPingReqPacketAsync(cancellationToken).ConfigureAwait(false);
                            break;

                        case MqttPingRespPacket:
                            throw new MqttProtocolViolationException("PINGRESP包只能由服务器发送");

                        case MqttDisconnectPacket disconnectPacket:
                            // 断开连接
                            this._logger.LogInformation(
                                "设备：{DeviceKey}，收到断开连接包：{pack}，ReasonCode：{ReasonCode}，ReasonString：{ReasonString}",
                                this.DeviceKey,
                                disconnectPacket.ToString(),
                                disconnectPacket.ReasonCode,
                                disconnectPacket.ReasonString);
                            return;

                        default:
                            throw new MqttProtocolViolationException("不支持的包");
                    }
                }
            }
            catch (ConnectionResetException)
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
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "解析MQTT包出错");
            }
        }

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
                                await this._packageHandler.HandleAsync(this, package).ConfigureAwait(false);
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

        #endregion

        #region 处理包

        /// <summary>
        /// 处理心跳包
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async ValueTask HandleIncomingPingReqPacketAsync(CancellationToken cancellationToken)
        {
            // 回复心跳
            var ackPack = MqttPingRespPacket.Instance;
            await this._mqttChannel.SendAsync(ackPack, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask HandleIncomingPubRelPacketAsync(MqttPubRelPacket pubRelPacket, CancellationToken cancellationToken)
        {
            // 回复
            var pubCompPacket = MqttPacketFactories.PubComp.Create(pubRelPacket, MqttApplicationMessageReceivedReasonCode.Success);
            await this._mqttChannel.SendAsync(pubCompPacket, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask HandleIncomingPubRecPacketAsync(MqttPubRecPacket pubRecPacket, CancellationToken cancellationToken)
        {
            // 回复
            var pubRelPacket = MqttPacketFactories.PubRel.Create(pubRecPacket, MqttApplicationMessageReceivedReasonCode.Success);
            await this._mqttChannel.SendAsync(pubRelPacket, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask HandleIncomingSubscribePacketAsync(MqttSubscribePacket subscribePacket, CancellationToken cancellationToken)
        {
            var subscribeResult = new SubscribeResult(subscribePacket.TopicFilters.Count);
            foreach (var topicFilterItem in subscribePacket.TopicFilters.OrderByDescending(f => f.QualityOfServiceLevel))
            {
                // TODO：默认允许订阅所有
                if (topicFilterItem.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtMostOnce)
                {
                    subscribeResult.ReasonCodes.Add(MqttSubscribeReasonCode.GrantedQoS0);
                }
                else if (topicFilterItem.QualityOfServiceLevel == MqttQualityOfServiceLevel.AtLeastOnce)
                {
                    subscribeResult.ReasonCodes.Add(MqttSubscribeReasonCode.GrantedQoS1);
                }
                else if (topicFilterItem.QualityOfServiceLevel == MqttQualityOfServiceLevel.ExactlyOnce)
                {
                    subscribeResult.ReasonCodes.Add(MqttSubscribeReasonCode.GrantedQoS2);
                }

                if (topicFilterItem.Topic.StartsWith("$share/"))
                {
                    subscribeResult.ReasonCodes.Add(MqttSubscribeReasonCode.SharedSubscriptionsNotSupported);
                }
            }

            // 回复
            var subAckPacket = MqttPacketFactories.SubAck.Create(subscribePacket, subscribeResult);
            await this._mqttChannel.SendAsync(subAckPacket, cancellationToken).ConfigureAwait(false);
        }

        private async ValueTask HandleIncomingUnsubscribePacket(MqttUnsubscribePacket unsubscribePacket, CancellationToken cancellationToken)
        {
            var unsubscribeResult = new UnsubscribeResult();
            foreach (var topicFilter in unsubscribePacket.TopicFilters)
            {
                unsubscribeResult.ReasonCodes.Add(MqttUnsubscribeReasonCode.Success);
            }

            var unsubAckPacket = MqttPacketFactories.UnsubAck.Create(unsubscribePacket, unsubscribeResult);
            await this._mqttChannel.SendAsync(unsubAckPacket, cancellationToken).ConfigureAwait(false);
        }

        //private async ValueTask HandleIncomingPubCompPacket(MqttPubCompPacket pubCompPacket, CancellationToken cancellationToken)
        //{
        //    //var acknowledgedPublishPacket = 
        //}

        private async ValueTask HandleIncomingPublishPacketAsync(MqttPublishPacket publishPacket, CancellationToken cancellationToken)
        {
            this._packageChannel.Writer.TryWrite(new MqttPackage(this.DeviceKey, publishPacket.PayloadSegment)
            {
                QualityOfServiceLevel = publishPacket.QualityOfServiceLevel,
                Topic = publishPacket.Topic
            });

            // 回复设备
            switch (publishPacket.QualityOfServiceLevel)
            {
                case MqttQualityOfServiceLevel.AtMostOnce:
                    // 不用处理

                    break;
                case MqttQualityOfServiceLevel.AtLeastOnce:
                    var pubAckPacket = new MqttPubAckPacket
                    {
                        PacketIdentifier = publishPacket.PacketIdentifier,
                        ReasonCode = MqttPubAckReasonCode.Success
                    };
                    await this._mqttChannel.SendAsync(pubAckPacket, cancellationToken).ConfigureAwait(false);

                    break;
                case MqttQualityOfServiceLevel.ExactlyOnce:
                    var pubRecPacket = new MqttPubRecPacket
                    {
                        PacketIdentifier = publishPacket.PacketIdentifier,
                        ReasonCode = MqttPubRecReasonCode.Success
                    };
                    await this._mqttChannel.SendAsync(pubRecPacket, cancellationToken).ConfigureAwait(false);

                    break;
                default:
                    throw new MqttCommunicationException("收到不支持的 QoS level");
            }
        }

        #endregion
    }
}
