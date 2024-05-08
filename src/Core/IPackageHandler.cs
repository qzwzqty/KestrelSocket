using System.Diagnostics.CodeAnalysis;

namespace KestrelSocket.Core
{
    /// <summary>
    /// 消息处理
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public interface IPackageHandler<TPackage>
        where TPackage : PackageBase
    {
        /// <summary>
        /// 消息处理
        /// </summary>
        /// <typeparam name="TSession"></typeparam>
        /// <param name="session"></param>
        /// <param name="package"></param>
        /// <returns></returns>
        ValueTask HandleAsync<TSession>([NotNull] TSession session, [NotNull] TPackage package)
            where TSession : IDeviceSession;
    }
}
