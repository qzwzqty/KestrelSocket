namespace KestrelSocket.Core
{
    /// <summary>
    /// Session关闭类型
    /// </summary>
    public enum SessionCloseReasonType
    {
        /// <summary>
        /// 客户端主动关闭
        /// </summary>
        RemoteClose,

        /// <summary>
        /// 服务端强制关闭
        /// </summary>
        ServerClose,

        /// <summary>
        /// 服务器停止
        /// </summary>
        SeverShutdown,

        /// <summary>
        /// 超时
        /// </summary>
        Timeout,

        /// <summary>
        /// 表示该设备Session已过期
        /// </summary>
        SessionExpired
    }
}
