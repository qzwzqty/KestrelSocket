namespace KestrelSocket.Core
{
    /// <summary>
    /// 配置
    /// </summary>
    public class KestrelSocketCoreOptions
    {
        /// <summary>
        /// 执行清理Session间隔，默认2分钟
        /// </summary>
        public int ClearIdleSessionInterval { get; set; } = 120;

        /// <summary>
        /// Session超时时间，默认5分钟
        /// </summary>
        public int IdleSessionTimeout { get; set; } = 300;

        /// <summary>
        /// 最大数据包，默认5M
        /// </summary>
        public int MaxPackageLength { get; set; } = 5242880;
    }
}
