using System.Buffers;

namespace KestrelSocket.Core
{
    /// <summary>
    /// 解析包
    /// </summary>
    public interface IPackageDecoder<TPackage>
        where TPackage : PackageBase
    {
        /// <summary>
        /// 解析
        /// </summary>
        /// <param name="input"></param>
        /// <param name="package"></param>
        /// <param name="consumed"></param>
        /// <param name="examined"></param>
        /// <returns></returns>
        bool TryDecode(in ReadOnlySequence<byte> input,
            out TPackage? package,
            out SequencePosition consumed,
            out SequencePosition examined);
    }
}
