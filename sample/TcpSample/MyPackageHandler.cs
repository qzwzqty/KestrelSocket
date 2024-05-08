using System.Diagnostics.CodeAnalysis;
using KestrelSocket.Core;

namespace TcpSample
{
    public class MyPackageHandler : IPackageHandler<MyPackage>
    {
        private readonly ILogger<MyPackageHandler> _logger;

        public MyPackageHandler(ILogger<MyPackageHandler> logger)
        {
            this._logger = logger;
        }

        public ValueTask HandleAsync<TSession>([NotNull] TSession session, [NotNull] MyPackage package) where TSession : IDeviceSession
        {
            this._logger.LogInformation("收到数据：{de},{mes}", session.DeviceKey, package.ToString());

            session.AddProperty<string>("aaa", "ddddd");
            session.TryGetProperty<string>("aaa", out var aaa);

            session.AddProperty<MyClass>("dadad", new MyClass() { Name = "aaa" });
            session.TryGetProperty<MyClass>("dadad", out var dadad);

            session.AddProperty<byte>("aaa1", 1);
            session.TryGetProperty<byte?>("aaa1", out var aaa1);

            return ValueTask.CompletedTask;
        }
    }

    public class MyClass
    {
        public string? Name { get; set; }
    }
}
