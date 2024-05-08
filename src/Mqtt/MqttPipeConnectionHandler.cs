using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// MqttPipeConnectionHandler
    /// </summary>
    public class MqttPipeConnectionHandler(
        IDeviceSessionManager deviceSessionManager,
        IOptions<KestrelSocketCoreOptions> options,
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory) : ConnectionHandler
    {
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly ILoggerFactory _loggerFactory = loggerFactory;
        private readonly int _maxPackageLength = options.Value.MaxPackageLength;
        private readonly IDeviceSessionManager _deviceSessionManager = deviceSessionManager;

        /// <summary>
        /// OnConnectedAsync
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            var transferFormatFeature = connection.Features.Get<ITransferFormatFeature>();
            if (transferFormatFeature != null)
            {
                transferFormatFeature.ActiveFormat = TransferFormat.Binary;
            }

            // 创建Channel和Session
            await using var channel = new MqttPipeChannel(connection, this._maxPackageLength, this._loggerFactory);
            var session = ActivatorUtilities.CreateInstance<DefaultMqttDeviceSession>(this._serviceProvider, connection.ConnectionId, channel);
            await using (session)
            {
                var sessionStarted = await session.StartAsync().ConfigureAwait(false);
                if (sessionStarted)
                {
                    // 添加Session
                    await this._deviceSessionManager.ConnectionAsync(session).ConfigureAwait(false);
                }
            }

            // 执行到这里，会关闭连接
            if (!session.SessionExpired)
            {
                await this._deviceSessionManager.DisconnectionAsync(session.DeviceKey).ConfigureAwait(false);
            }
        }
    }
}
