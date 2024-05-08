using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// 配置
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加MQTT库
        /// </summary>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="services"></param>
        /// <param name="port"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddKestrelMqtt<TPackageHandler>(
            this IServiceCollection services,
            int port,
            IConfiguration configuration)
            where TPackageHandler : class, IPackageHandler<MqttPackage>
        {
            services.AddKestrelSocketCore<MqttPackage, NullPackageDecoder, TPackageHandler>(configuration);
            services.Configure<KestrelServerOptions>(opt =>
            {
                opt.ListenAnyIP(port, config =>
                    config.UseConnectionLogging("KestrelSocket.Mqtt.ConnectionLogging").UseConnectionHandler<MqttPipeConnectionHandler>());
            });

            return services;
        }

        /// <summary>
        /// 添加MQTT库
        /// </summary>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="hostBuilder"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static IHostBuilder AddKestrelMqtt<TPackageHandler>(this IHostBuilder hostBuilder, int port)
            where TPackageHandler : class, IPackageHandler<MqttPackage>
        {
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddKestrelMqtt<TPackageHandler>(port, ctx.Configuration);
            });

            return hostBuilder;
        }
    }
}
