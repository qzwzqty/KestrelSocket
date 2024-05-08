namespace KestrelSocket.Core
{
    /// <summary>
    /// 设备连接的通道
    /// </summary>
    public interface IChannel : IAsyncDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        string ChannelId { get; }

        /// <summary>
        /// 
        /// </summary>
        string? Endpoint { get; }

        /// <summary>
        /// 
        /// </summary>
        bool IsClosed { get; }

        /// <summary>
        /// 通道最近收到数据的时间
        /// </summary>
        DateTimeOffset LastActiveTime { get; }

        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="data"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        ValueTask SendAsync(ReadOnlyMemory<byte> data, CancellationToken cancellationToken = default);
    }
}
