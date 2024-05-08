namespace KestrelSocket.Core
{
    /// <summary>
    /// DeviceSessionManager
    /// </summary>
    public interface IDeviceSessionManager
    {
        /// <summary>
        /// Session连接，同一个DeviceKey，会顶掉旧的Session，并且旧的Session会被关闭
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        Task ConnectionAsync(IDeviceSession session);

        /// <summary>
        /// 移除Session，会关闭Session
        /// </summary>
        /// <param name="deviceKey"></param>
        /// <returns></returns>
        Task DisconnectionAsync(string deviceKey);

        /// <summary>
        /// 获取Session
        /// </summary>
        /// <param name="deviceKey"></param>
        /// <returns></returns>
        IDeviceSession? Get(string deviceKey);

        /// <summary>
        /// 获取所有Session
        /// </summary>
        /// <returns></returns>
        List<IDeviceSession> GetSessions();

        /// <summary>
        /// 注册设备连接/断开处理程序
        /// </summary>
        /// <param name="func"></param>
        void RegisterConnectionListener(Func<IDeviceSession, ConnectionState, Task> func);
    }

    /// <summary>
    /// 连接状态
    /// </summary>
    public enum ConnectionState
    {
        /// <summary>
        /// 首次连接
        /// </summary>
        Connect,

        /// <summary>
        /// 断开连接
        /// </summary>
        Disconnect,

        /// <summary>
        /// 重新连接，
        /// 这种情况是：同一个DeviceKey替换已存在的Session的时候
        /// </summary>
        Reconnect
    }
}
