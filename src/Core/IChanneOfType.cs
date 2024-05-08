namespace KestrelSocket.Core
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public interface IChannel<TPackage> : IChannel
        where TPackage : PackageBase
    {
        /// <summary>
        /// 接收消息
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task<TPackage?> ReceivePacketAsync(CancellationToken cancellationToken);
    }
}
