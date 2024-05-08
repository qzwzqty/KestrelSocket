using System.Diagnostics.CodeAnalysis;
using System.Text;
using KestrelSocket.Core;
using KestrelSocket.Mqtt;

namespace MqttSample
{
    public class MyMqttHandler : IPackageHandler<MqttPackage>
    {
        private readonly ILogger<MyMqttHandler> _logger;

        public MyMqttHandler(ILogger<MyMqttHandler> logger)
        {
            this._logger = logger;
        }

        public ValueTask HandleAsync<TSession>([NotNull] TSession session, [NotNull] MqttPackage package) where TSession : IDeviceSession
        {
            this._logger.LogInformation(
                "收到数据：{de},{mes}，topic：{topic}, qos:{qos}",
                session.DeviceKey,
                Encoding.UTF8.GetString(package.Payload),
                package.Topic,
                package.QualityOfServiceLevel);

            return ValueTask.CompletedTask;
        }
    }
}
