using System.Buffers;
using KestrelSocket.Core;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// 不应该使用
    /// </summary>
    internal class NullPackageDecoder : IPackageDecoder<MqttPackage>
    {
        public bool TryDecode(in ReadOnlySequence<byte> input, out MqttPackage? package, out SequencePosition consumed, out SequencePosition examined)
        {
            throw new NotImplementedException();
        }
    }
}
