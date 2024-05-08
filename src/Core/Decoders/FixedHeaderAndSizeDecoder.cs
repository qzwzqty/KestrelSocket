using System.Buffers;

namespace KestrelSocket.Core.Decoders
{
    /// <summary>
    /// 长度类型
    /// </summary>
    public enum LengthType
    {
        /// <summary>
        /// Short
        /// </summary>
        Short,

        /// <summary>
        /// Int
        /// </summary>
        Int,

        /// <summary>
        /// Long
        /// </summary>
        Long
    }

    /// <summary>
    /// 头部格式固定并且包含内容长度的协议
    /// +-------+------+---+-------------------------------+
    /// |request|off   | l |                               |
    /// | name  |len   | e |    request body               |
    /// |  (4)  |(2)   | n |                               |
    /// |       |.     |(2)|                               |
    /// +-------+------+---+-------------------------------+
    /// </summary>
    public abstract class FixedHeaderAndSizeDecoder<TPackage> : PackageDecoderBase<TPackage>
        where TPackage : PackageBase
    {
        private readonly ReadOnlyMemory<byte> _beginMark;
        private readonly int _offset;
        private readonly bool _bigEndian;
        private readonly LengthType _lengthType;
        private ReadState _readState = ReadState.BeginMark;

        private enum ReadState
        {
            BeginMark,

            Offset,

            Length,

            Body
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="beginMark"></param>
        /// <param name="offset"></param>
        /// <param name="bigEndian"></param>
        /// <param name="lengthType"></param>
        public FixedHeaderAndSizeDecoder(
            ReadOnlyMemory<byte> beginMark,
            int offset,
            bool bigEndian,
            LengthType lengthType)
        {
            this._beginMark = beginMark;
            this._offset = offset;
            this._bigEndian = bigEndian;
            this._lengthType = lengthType;
        }

        /// <summary>
        /// 解析
        /// </summary>
        /// <param name="input"></param>
        /// <param name="package"></param>
        /// <param name="consumed"></param>
        /// <param name="examined"></param>
        /// <returns></returns>
        protected override bool TryDecode(in ReadOnlySequence<byte> input, out TPackage? package, out SequencePosition consumed, out SequencePosition examined)
        {
            long bodyLength = 0;
            var reader = new SequenceReader<byte>(input);
            while (true)
            {
                switch (this._readState)
                {
                    case FixedHeaderAndSizeDecoder<TPackage>.ReadState.BeginMark:
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

                        this._readState = ReadState.Offset;

                        break;
                    case FixedHeaderAndSizeDecoder<TPackage>.ReadState.Offset:
                        // 判断offset，offset之后就是body长度
                        if (this._offset > 0)
                        {
                            if (reader.Remaining <= this._offset)
                            {
                                consumed = input.GetPosition(reader.Consumed);
                                examined = input.End;
                                package = null;

                                return false;
                            }

                            reader.Advance(this._offset);
                        }

                        this._readState = ReadState.Length;

                        break;
                    case FixedHeaderAndSizeDecoder<TPackage>.ReadState.Length:
                        // 获取Body长度
                        switch (this._lengthType)
                        {
                            case LengthType.Int:
                                int l1;
                                if (this._bigEndian ? reader.TryReadBigEndian(out l1) : reader.TryReadLittleEndian(out l1))
                                {
                                    bodyLength = l1;
                                }
                                else
                                {
                                    // 说明数据不够
                                    consumed = input.GetPosition(reader.Consumed);
                                    examined = input.End;
                                    package = null;

                                    return false;
                                }

                                break;
                            case LengthType.Long:
                                long l2;
                                if (this._bigEndian ? reader.TryReadBigEndian(out l2) : reader.TryReadLittleEndian(out l2))
                                {
                                    bodyLength = l2;
                                }
                                else
                                {
                                    // 说明数据不够
                                    consumed = input.GetPosition(reader.Consumed);
                                    examined = input.End;
                                    package = null;

                                    return false;
                                }

                                break;
                            default:
                                // 默认short
                                short l3;
                                if (this._bigEndian ? reader.TryReadBigEndian(out l3) : reader.TryReadLittleEndian(out l3))
                                {
                                    bodyLength = l3;
                                }
                                else
                                {
                                    // 说明数据不够
                                    consumed = input.GetPosition(reader.Consumed);
                                    examined = input.End;
                                    package = null;

                                    return false;
                                }

                                break;
                        }

                        this._readState = ReadState.Body;

                        break;
                    case FixedHeaderAndSizeDecoder<TPackage>.ReadState.Body:

                        // 读取Body
                        if (bodyLength >= 0)
                        {
                            if (reader.Remaining < bodyLength)
                            {
                                // 说明数据不够
                                consumed = input.GetPosition(reader.Consumed);
                                examined = input.End;
                                package = null;

                                return false;
                            }

                            // 说明剩余的数据足够了
                            var bodyBuffer = reader.UnreadSequence.Slice(0, bodyLength);
                            package = this.DecodePackage(ref bodyBuffer);
                            reader.Advance(bodyLength);
                            examined = consumed = input.GetPosition(reader.Consumed);
                            this._readState = ReadState.BeginMark;
                            return true;
                        }

                        break;
                }
            }
        }
    }
}
