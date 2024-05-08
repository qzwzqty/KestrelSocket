namespace KestrelSocket.Core
{
    /// <summary>
    /// 基类
    /// </summary>
    /// <param name="deviceKey"></param>
    public abstract class PackageBase(string deviceKey)
    {
        /// <summary>
        /// 设备唯一标识
        /// </summary>
        public string DeviceKey { get; } = deviceKey;
    }
}
