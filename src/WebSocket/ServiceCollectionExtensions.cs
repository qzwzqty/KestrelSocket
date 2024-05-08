using System.Net.WebSockets;
using KestrelSocket.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KestrelSocket.WebSocket
{
    /// <summary>
    /// 
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加WebSocket，
        /// 需要在管道中引用：<see cref="UseKestrelWebSocket"/>
        /// </summary>
        /// <typeparam name="TPackage"></typeparam>
        /// <typeparam name="TPackageDecoder"></typeparam>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddKestrelWebSocket<TPackage, TPackageDecoder, TPackageHandler>(
            this IServiceCollection services,
            IConfiguration configuration)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            services.AddKestrelSocketCore<TPackage, TPackageDecoder, TPackageHandler>(configuration);

            return services;
        }

        /// <summary>
        /// 添加WebSocket，
        /// 需要在管道中引用：<see cref="UseKestrelWebSocket"/>
        /// </summary>
        /// <typeparam name="TPackage"></typeparam>
        /// <typeparam name="TPackageDecoder"></typeparam>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="hostBuilder"></param>
        /// <returns></returns>
        public static IHostBuilder AddKestrelWebSocket<TPackage, TPackageDecoder, TPackageHandler>(this IHostBuilder hostBuilder)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddKestrelWebSocket<TPackage, TPackageDecoder, TPackageHandler>(ctx.Configuration);
            });

            return hostBuilder;
        }

        /// <summary>
        /// 添加WebSocket中间件，
        /// 需要引入WebSocket, app.UseWebSockets() 
        /// </summary>
        /// <typeparam name="TPackage"></typeparam>
        /// <param name="app"></param>
        /// <param name="patterns"></param>
        /// <param name="webSocketMessageType"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseKestrelWebSocket<TPackage>(
            this IApplicationBuilder app,
            string[]? patterns = null,
            WebSocketMessageType webSocketMessageType = WebSocketMessageType.Text)
        where TPackage : PackageBase
        {
            app.UseMiddleware<WebSocketMiddleware<TPackage>>(patterns, webSocketMessageType);

            return app;
        }
    }
}
