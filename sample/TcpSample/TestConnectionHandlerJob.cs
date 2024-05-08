using KestrelSocket.Core;

namespace TcpSample
{
    public class TestConnectionHandlerJob : BackgroundService
    {
        private readonly IDeviceSessionManager _deviceSessionManager;
        private readonly ILogger<TestConnectionHandlerJob> _logger;

        public TestConnectionHandlerJob(
            IDeviceSessionManager deviceSessionManager,
            ILogger<TestConnectionHandlerJob> logger)
        {
            this._deviceSessionManager = deviceSessionManager;
            this._logger = logger;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this._deviceSessionManager.RegisterConnectionListener(this.ConnectionHandlerAsync);

            return Task.CompletedTask;
        }

        private async Task ConnectionHandlerAsync(IDeviceSession deviceSession, ConnectionState connectionState)
        {
            await Task.Delay(10000);
            this._logger.LogInformation("设备：{d}，收到状态：{s}", deviceSession.DeviceKey, connectionState);
        }
    }
}
