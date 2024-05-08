using System.Collections.Concurrent;

namespace KestrelSocket.Core
{
    /// <summary>
    /// Session
    /// </summary>
    public interface IDeviceSession : IAsyncDisposable
    {
        /// <summary>
        /// 其他属性
        /// </summary>
        ConcurrentDictionary<string, IPropertyValue> Properties { get; }

        /// <summary>
        /// 通道
        /// </summary>
        IChannel Channel { get; }

        /// <summary>
        /// 表示Session是否过期
        /// </summary>
        bool SessionExpired { get; set; }

        /// <summary>
        /// 设备Key
        /// </summary>
        string DeviceKey { get; }

        /// <summary>
        /// 已启动
        /// </summary>
        bool Started { get; }

        /// <summary>
        /// 已关闭
        /// </summary>
        bool Closed { get; }

        /// <summary>
        /// 连接时间
        /// </summary>
        DateTimeOffset ConnectionTime { get; }

        /// <summary>
        /// 最近心跳时间
        /// </summary>
        DateTimeOffset LastHeartbeatTime { get; }

        /// <summary>
        /// 启动Session
        /// </summary>
        /// <returns></returns>
        Task<bool> StartAsync();

        /// <summary>
        /// 关闭
        /// </summary>
        /// <param name="reason"></param>
        /// <returns></returns>
        Task CloseAsync(SessionCloseReasonType reason);
    }

    /// <summary>
    /// 属性值
    /// </summary>
    public interface IPropertyValue
    {
    }

    /// <summary>
    /// 属性值
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public struct PropertyValue<T> : IPropertyValue
    {
        /// <summary>
        /// 值
        /// </summary>
        public T Value { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        public PropertyValue(T value)
        {
            this.Value = value;
        }
    }

    /// <summary>
    /// 
    /// </summary>
    public static class DeviceSessionExtensions
    {
        /// <summary>
        /// 添加属性
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="deviceSession"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public static void AddProperty<TValue>(this IDeviceSession deviceSession, string key, TValue value)
        {
            var propertyValue = new PropertyValue<TValue>(value);
            deviceSession.Properties.AddOrUpdate(key, propertyValue, (_, _) =>
            {
                return propertyValue;
            });
        }

        /// <summary>
        /// 获取属性
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="deviceSession"></param>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public static bool TryGetProperty<TValue>(this IDeviceSession deviceSession, string key, out TValue? value)
        {
            if (deviceSession.Properties.TryGetValue(key, out var property))
            {
                if (property is PropertyValue<TValue> pv)
                {
                    value = pv.Value;
                    return true;
                }
            }

            value = default;
            return false;
        }
    }
}
