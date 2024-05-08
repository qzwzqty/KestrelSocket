using KestrelSocket.Core;

namespace WebSocketSample
{
    public class MyPackage(string deviceKey)
        : PackageBase(deviceKey)
    {
        public string? Data { get; set; }

        public override string ToString()
        {
            return this.Data!;
        }
    }
}
