using KestrelSocket.Core;
using MQTTnet.Packets;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// Mqtt Channel
    /// </summary>
    public interface IMqttChannel : IChannel
    {
        /// <summary>
        /// 获取Mqtt Package
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<MqttPacket?> ReceivePacketAsync(CancellationToken cancellationToken);

        /// <summary>
        /// 发送Mqtt Package
        /// </summary>
        /// <param name="packet"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask SendAsync(MqttPacket packet, CancellationToken cancellationToken);
    }
}
