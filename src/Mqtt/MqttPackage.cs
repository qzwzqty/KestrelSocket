using KestrelSocket.Core;
using MQTTnet.Protocol;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// MqttPackage
    /// </summary>
    /// <param name="deviceKey"></param>
    /// <param name="payload"></param>
    public class MqttPackage(
        string deviceKey,
        ArraySegment<byte> payload) : PackageBase(deviceKey)
    {

        /// <summary>
        /// 数据
        /// </summary>
        public ArraySegment<byte> Payload { get; set; } = payload;

        /// <summary>
        /// Topic
        /// </summary>
        public string? Topic { get; set; }

        /// <summary>
        /// QOS
        /// </summary>
        public MqttQualityOfServiceLevel QualityOfServiceLevel { get; set; } = MqttQualityOfServiceLevel.AtMostOnce;
    }
}
