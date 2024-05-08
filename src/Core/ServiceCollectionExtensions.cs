using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace KestrelSocket.Core
{
    /// <summary>
    /// 配置
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加Socket核心服务
        /// </summary>
        /// <typeparam name="TPackage"></typeparam>
        /// <typeparam name="TPackageDecoder"></typeparam>
        /// <typeparam name="TPackageHandler"></typeparam>
        /// <param name="services"></param>
        /// <param name="configuration"></param>
        /// <returns></returns>
        public static IServiceCollection AddKestrelSocketCore<TPackage, TPackageDecoder, TPackageHandler>(
            this IServiceCollection services,
            IConfiguration configuration)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            services.AddOptions<KestrelSocketCoreOptions>()
                    .Bind(configuration.GetSection("KestrelSocket:Socket"))
                    .ValidateDataAnnotations();

            services.AddTransient<IPackageDecoder<TPackage>, TPackageDecoder>();
            services.AddSingleton<IPackageHandler<TPackage>, TPackageHandler>();
            services.TryAddSingleton<IDeviceSessionManager, DefaultDeviceSessionManager>();
            services.AddHostedService<ClearIdleSessionJob>();

            return services;
        }

        /// <summary>
        /// 添加SocketCore库
        /// </summary>
        /// <param name="hostBuilder"></param>
        /// <returns></returns>
        public static IHostBuilder AddKestrelSocketCore<TPackage, TPackageDecoder, TPackageHandler>(this IHostBuilder hostBuilder)
            where TPackage : PackageBase
            where TPackageDecoder : class, IPackageDecoder<TPackage>
            where TPackageHandler : class, IPackageHandler<TPackage>
        {
            hostBuilder.ConfigureServices((ctx, services) =>
            {
                services.AddKestrelSocketCore<TPackage, TPackageDecoder, TPackageHandler>(ctx.Configuration);
            });

            return hostBuilder;
        }
    }
}
