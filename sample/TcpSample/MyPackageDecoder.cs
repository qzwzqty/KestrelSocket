using System.Buffers;
using System.Text;
using KestrelSocket.Core;

namespace TcpSample
{
    public class MyPackageDecoder : IPackageDecoder<MyPackage>
    {
        public bool TryDecode(in ReadOnlySequence<byte> input, out MyPackage? package, out SequencePosition consumed, out SequencePosition examined)
        {
            var pack = new MyPackage("test1")
            {
                Data = Encoding.UTF8.GetString(input)
            };

            consumed = input.End;
            examined = input.End;
            package = pack;
            return true;
        }
    }
}
