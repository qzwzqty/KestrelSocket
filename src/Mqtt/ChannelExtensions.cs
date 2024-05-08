using KestrelSocket.Core;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// MQTT Channel扩展方法
    /// </summary>
    public static class ChannelExtensions
    {
        /// <summary>
        /// 发送数据到指定Topic
        /// </summary>
        /// <param name="channel"></param>
        /// <param name="topic"></param>
        /// <param name="data"></param>
        /// <param name="qos"></param>
        /// <returns></returns>
        public static async ValueTask SendAsync(
            this IChannel channel,
            string topic,
            byte[] data,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce)
        {
            if (channel is IMqttChannel mqttChannel)
            {
                var mqttApplicationMessage = new MqttApplicationMessageBuilder()
                    .WithPayload(data)
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build();
                var publishPacketCopy = MqttPacketFactories.Publish.Create(mqttApplicationMessage);
                await mqttChannel.SendAsync(publishPacketCopy, default);
            }
        }
    }
}
