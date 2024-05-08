using System.Buffers;

namespace KestrelSocket.Core.Decoders
{
    /// <summary>
    /// 固定长度
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public abstract class FixedSizeDecoder<TPackage> : PackageDecoderBase<TPackage>
        where TPackage : PackageBase
    {
        private readonly int _size;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="size"></param>
        public FixedSizeDecoder(int size)
        {
            this._size = size;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="package"></param>
        /// <param name="consumed"></param>
        /// <param name="examined"></param>
        /// <returns></returns>
        protected override bool TryDecode(in ReadOnlySequence<byte> input, out TPackage? package, out SequencePosition consumed, out SequencePosition examined)
        {
            if (input.Length < this._size)
            {
                package = null;
                consumed = input.Start;
                examined = input.End;

                return false;
            }

            var reader = new SequenceReader<byte>(input);
            var buffer = reader.UnreadSequence.Slice(0, this._size);
            package = this.DecodePackage(ref buffer);
            reader.Advance(this._size);
            examined = consumed = input.GetPosition(reader.Consumed);

            return true;
        }
    }
}
