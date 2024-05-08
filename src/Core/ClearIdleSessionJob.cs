using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KestrelSocket.Core
{
    internal class ClearIdleSessionJob : BackgroundService
    {
        private readonly IDeviceSessionManager _deviceSessionManager;
        private readonly ILogger<ClearIdleSessionJob> _logger;
        private readonly KestrelSocketCoreOptions _options;

        public ClearIdleSessionJob(
            IDeviceSessionManager deviceSessionManager,
            IOptions<KestrelSocketCoreOptions> options,
            ILogger<ClearIdleSessionJob> logger,
            IHostApplicationLifetime hostApplicationLifetime)
        {
            this._deviceSessionManager = deviceSessionManager;
            this._logger = logger;
            this._options = options.Value;

            hostApplicationLifetime.ApplicationStopping.Register(this.StopAllSession);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _ = Task.Factory.StartNew(async () =>
            {
                await Task.Yield();

                // 第一次运行等待
                await Task.Delay(TimeSpan.FromSeconds(this._options.ClearIdleSessionInterval), stoppingToken).ConfigureAwait(false);

                while (!stoppingToken.IsCancellationRequested)
                {
                    var timeoutTime = DateTimeOffset.Now.AddSeconds(0 - this._options.IdleSessionTimeout);
                    foreach (var item in this._deviceSessionManager.GetSessions())
                    {
                        if (item.LastHeartbeatTime <= timeoutTime)
                        {
                            try
                            {
                                await item.CloseAsync(SessionCloseReasonType.Timeout);
                                this._logger.LogInformation("设备:{DeviceKey}将被关闭，LastActiveTime：{LastActiveTime}", item.DeviceKey, item.LastHeartbeatTime);
                            }
                            catch
                            {
                            }
                        }
                    }

                    await Task.Delay(TimeSpan.FromSeconds(this._options.ClearIdleSessionInterval), stoppingToken).ConfigureAwait(false);
                }
            }, stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

            return Task.CompletedTask;
        }

        /// <summary>
        /// 关闭所有连接
        /// </summary>
        private void StopAllSession()
        {
            foreach (var item in this._deviceSessionManager.GetSessions())
            {
                item.CloseAsync(SessionCloseReasonType.SeverShutdown);
            }
        }
    }
}
