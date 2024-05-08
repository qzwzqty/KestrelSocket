using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KestrelSocket.Tcp
{
    /// <summary>
    /// TCP处理
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public class TcpPipeConnectionHandler<TPackage> : ConnectionHandler
        where TPackage : PackageBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly int _maxPackageLength;
        private readonly IDeviceSessionManager _deviceSessionManager;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceSessionManager"></param>
        /// <param name="options"></param>
        /// <param name="serviceProvider"></param>
        public TcpPipeConnectionHandler(
            IDeviceSessionManager deviceSessionManager,
            IOptions<KestrelSocketCoreOptions> options,
            IServiceProvider serviceProvider)
        {
            this._deviceSessionManager = deviceSessionManager;
            this._serviceProvider = serviceProvider;
            this._maxPackageLength = options.Value.MaxPackageLength;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public override async Task OnConnectedAsync(ConnectionContext connection)
        {
            // 创建Channel和Session
            using var scope = this._serviceProvider.CreateScope();
            var packageDecoder = scope.ServiceProvider.GetRequiredService<IPackageDecoder<TPackage>>();

            await using var channel = new TcpPipeChannel<TPackage>(connection, packageDecoder, this._maxPackageLength);
            var session = ActivatorUtilities.CreateInstance<DefaultDeviceSession<TPackage>>(this._serviceProvider, connection.ConnectionId, channel);
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
