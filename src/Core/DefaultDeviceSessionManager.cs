using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace KestrelSocket.Core
{
    /// <summary>
    /// 
    /// </summary>
    public class DefaultDeviceSessionManager(
        ILogger<DefaultDeviceSessionManager> logger)
        : IDeviceSessionManager
    {
        private readonly ConcurrentDictionary<string, IDeviceSession> _deviceSessions = new();
        private readonly ILogger<DefaultDeviceSessionManager> _logger = logger;
        private readonly List<Func<IDeviceSession, ConnectionState, Task>> _listenerFuncs = [];

        /// <summary>
        /// 添加Session
        /// </summary>
        /// <param name="session"></param>
        /// <returns></returns>
        public async Task ConnectionAsync(IDeviceSession session)
        {
            var deviceKey = session.DeviceKey;
            var connectionState = ConnectionState.Connect;

            // 判断该Session是否存在
            this._deviceSessions.TryGetValue(deviceKey, out var oldSession);
            if (oldSession != null)
            {
                this._logger.LogInformation("设备：{DeviceKey} 已有Session，将会断开旧Session", deviceKey);

                // 关闭旧Session
                oldSession.SessionExpired = true;
                connectionState = ConnectionState.Reconnect;
                await oldSession.CloseAsync(SessionCloseReasonType.SessionExpired);
            }

            // 添加新Session
            this._deviceSessions[deviceKey] = session;

            this._logger.LogDebug("设备：{DeviceKey} 连接成功", deviceKey);

            // 调用处理程序
            if (this._listenerFuncs.Count != 0)
            {
                foreach (var func in this._listenerFuncs)
                {
                    _ = func(session, connectionState);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceKey"></param>
        /// <returns></returns>
        public async Task DisconnectionAsync(string deviceKey)
        {
            if (this._deviceSessions.TryRemove(deviceKey, out var session) && session != null)
            {
                this._logger.LogDebug("设备：{DeviceKey} 将会断开连接", deviceKey);

                await session.CloseAsync(SessionCloseReasonType.RemoteClose).ConfigureAwait(false);

                // 调用处理程序
                if (this._listenerFuncs.Count != 0)
                {
                    foreach (var func in this._listenerFuncs)
                    {
                        _ = func(session, ConnectionState.Disconnect);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deviceKey"></param>
        /// <returns></returns>
        public IDeviceSession? Get(string deviceKey)
        {
            this._deviceSessions.TryGetValue(deviceKey, out var session);
            return session;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public List<IDeviceSession> GetSessions()
        {
            return [.. this._deviceSessions.Values];
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="func"></param>
        public void RegisterConnectionListener(Func<IDeviceSession, ConnectionState, Task> func)
        {
            this._listenerFuncs.Add(func);
        }
    }
}
