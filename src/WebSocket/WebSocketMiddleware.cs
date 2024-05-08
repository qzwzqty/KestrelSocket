using System.Net.WebSockets;
using KestrelSocket.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KestrelSocket.WebSocket
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public class WebSocketMiddleware<TPackage>(
        RequestDelegate next,
        IServiceProvider serviceProvider,
        IOptions<KestrelSocketCoreOptions> options,
        IDeviceSessionManager deviceSessionManager,
        string[]? patterns = null,
        WebSocketMessageType webSocketMessageType = WebSocketMessageType.Text)
        where TPackage : PackageBase
    {
        private readonly RequestDelegate _next = next;
        private readonly IServiceProvider _serviceProvider = serviceProvider;
        private readonly IDeviceSessionManager _deviceSessionManager = deviceSessionManager;
        private readonly string[]? _patterns = patterns;
        private readonly WebSocketMessageType _webSocketMessageType = webSocketMessageType;
        private readonly int _maxPackageLength = options.Value.MaxPackageLength;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task Invoke(HttpContext context)
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await this._next.Invoke(context).ConfigureAwait(false);
                return;
            }

            // 判断路径是否匹配
            if (this._patterns != null && this._patterns.Length != 0)
            {
                if (!this._patterns.Any(p => p == context.Request.Path))
                {
                    await this._next.Invoke(context);
                    return;
                }
            }

            // 开始处理WebSocket
            var connectId = context.Connection.Id;
            var remoteIpAddress = context.Connection.RemoteIpAddress?.ToString();
            var webSocket = await context.WebSockets.AcceptWebSocketAsync().ConfigureAwait(false);
            using var scope = this._serviceProvider.CreateScope();
            var packageDecoder = scope.ServiceProvider.GetRequiredService<IPackageDecoder<TPackage>>();
            await using var channel = new WebSocketPipeChannel<TPackage>(
                webSocket,
                packageDecoder,
                this._maxPackageLength,
                this._webSocketMessageType,
                connectId);
            channel.Endpoint = remoteIpAddress;
            _ = channel.StartReceivingAsync();
            var session = ActivatorUtilities.CreateInstance<DefaultDeviceSession<TPackage>>(this._serviceProvider, connectId, channel);
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
