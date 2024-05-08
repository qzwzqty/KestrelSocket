using KestrelSocket.Core;

namespace TcpSample
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
