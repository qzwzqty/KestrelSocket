using KestrelSocket.Core;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace KestrelSocket.Tcp
{
    /// <summary>
    /// 配置
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加TCP库
        /// </summary>
        /// <typeparam name="TPackage"></typeparam>
        /// <typeparam name="TPackageDecoder"></typeparam>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="services"></param>
        /// <param name="port"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddKestrelTcp<TPackage, TPackageDecoder, TPackageHandler>(
            this IServiceCollection services,
            int port,
            IConfiguration configuration)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            services.AddKestrelSocketCore<TPackage, TPackageDecoder, TPackageHandler>(configuration);
            services.Configure<KestrelServerOptions>(opt =>
            {
                opt.ListenAnyIP(port, config => config.UseConnectionLogging("KestrelSocket.Tcp.ConnectionLogging")
                    .UseConnectionHandler<TcpPipeConnectionHandler<TPackage>>());
            });

            return services;
        }

        /// <summary>
        /// 添加TCP库
        /// </summary>
        /// <param name="hostBuilder"></param>
        /// <param name="port"></param>
        /// <returns></returns>
        public static IHostBuilder AddKestrelTcp<TPackage, TPackageDecoder, TPackageHandler>(this IHostBuilder hostBuilder, int port)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddKestrelTcp<TPackage, TPackageDecoder, TPackageHandler>(port, ctx.Configuration);
            });

            return hostBuilder;
        }
    }
}
