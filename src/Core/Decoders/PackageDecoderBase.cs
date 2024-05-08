using System.Buffers;

namespace KestrelSocket.Core.Decoders
{
    /// <summary>
    /// 基类
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public abstract class PackageDecoderBase<TPackage> : IPackageDecoder<TPackage>
        where TPackage : PackageBase
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="package"></param>
        /// <param name="consumed"></param>
        /// <param name="examined"></param>
        /// <returns></returns>
        protected abstract bool TryDecode(in ReadOnlySequence<byte> input, out TPackage? package, out SequencePosition consumed, out SequencePosition examined);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <returns></returns>
        protected abstract TPackage DecodePackage(ref ReadOnlySequence<byte> buffer);

        bool IPackageDecoder<TPackage>.TryDecode(in ReadOnlySequence<byte> input, out TPackage? package, out SequencePosition consumed, out SequencePosition examined)
        {
            return this.TryDecode(in input, out package, out consumed, out examined);
        }
    }
}
