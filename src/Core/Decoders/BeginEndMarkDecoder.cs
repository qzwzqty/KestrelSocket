using System.Buffers;

namespace KestrelSocket.Core.Decoders
{
    /// <summary>
    /// 具有开始和结束帧的解码器
    /// </summary>
    /// <typeparam name="TPackage"></typeparam>
    public abstract class BeginEndMarkDecoder<TPackage> : PackageDecoderBase<TPackage>
        where TPackage : PackageBase
    {
        private readonly ReadOnlyMemory<byte> _beginMark;
        private readonly ReadOnlyMemory<byte> _endMark;
        private bool _foundBeginMark = false;

        /// <summary>
        /// 具有开始和结束帧的解码器
        /// </summary>
        /// <param name="beginMark"></param>
        /// <param name="endMark"></param>
        protected BeginEndMarkDecoder(ReadOnlyMemory<byte> beginMark, ReadOnlyMemory<byte> endMark)
        {
            this._beginMark = beginMark;
            this._endMark = endMark;
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
            var reader = new SequenceReader<byte>(input);

            // 开始读取数据
            if (!this._foundBeginMark)
            {
                // 判断未读取的数据
                if (reader.Remaining <= this._beginMark.Length)
                {
                    consumed = input.Start;
                    examined = input.End;
                    package = null;
                    return false;
                }

                // 找报文头
                var beginMark = this._beginMark.Span;
                if (!reader.TryReadTo(out ReadOnlySequence<byte> _, beginMark, advancePastDelimiter: true))
                {
                    // 如果找不到header，则跳过
                    reader.AdvanceToEnd();
                    examined = consumed = input.End;
                    package = null;
                    return false;
                }

                // 找到报文头
                this._foundBeginMark = true;
            }

            // 开始读取数据，一直读取到报文尾
            var endMark = this._endMark.Span;
            if (!reader.TryReadTo(out ReadOnlySequence<byte> buffer, endMark, advancePastDelimiter: true))
            {
                // 表示还需要等待读取更多数据
                consumed = input.GetPosition(reader.Consumed);
                examined = input.End;
                package = null;
                return false;
            }

            // 这里表示读取完成
            examined = consumed = input.GetPosition(reader.Consumed);
            package = this.DecodePackage(ref buffer);
            this._foundBeginMark = false;
            return true;
        }
    }
}
