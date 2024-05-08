using System.Buffers;
using System.Runtime.InteropServices;
using MQTTnet.Adapter;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;
using MQTTnet.Packets;

namespace KestrelSocket.Mqtt
{
    /// <summary>
    /// 扩展
    /// </summary>
    public static class ReaderExtensions
    {
        /// <summary>
        /// 解析MQTT 报文
        /// </summary>
        /// <param name="formatter"></param>
        /// <param name="input"></param>
        /// <param name="packet"></param>
        /// <param name="consumed"></param>
        /// <param name="observed"></param>
        /// <param name="bytesRead"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static bool TryDecode(this MqttPacketFormatterAdapter formatter,
            in ReadOnlySequence<byte> input,
            out MqttPacket? packet,
            out SequencePosition consumed,
            out SequencePosition observed,
            out int bytesRead)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            packet = null;
            consumed = input.Start;
            observed = input.End;
            bytesRead = 0;
            var copy = input;

            if (copy.Length < 2)
            {
                return false;
            }

            if (!TryReadBodyLength(ref copy, out var headerLength, out var bodyLength))
            {
                return false;
            }

            var fixedHeader = copy.First.Span[0];
            copy = copy.Slice(headerLength);
            if (copy.Length < bodyLength)
            {
                return false;
            }

            var bodySlice = copy.Slice(0, bodyLength);
            var bodySegment = GetArraySegment(ref bodySlice);

            var receivedMqttPacket = new ReceivedMqttPacket(fixedHeader, bodySegment, headerLength + bodyLength);
            if (formatter.ProtocolVersion == MqttProtocolVersion.Unknown)
            {
                formatter.DetectProtocolVersion(receivedMqttPacket);
            }

            packet = formatter.Decode(receivedMqttPacket);
            consumed = bodySlice.End;
            observed = bodySlice.End;
            bytesRead = headerLength + bodyLength;
            return true;
        }

        private static ArraySegment<byte> GetArraySegment(ref ReadOnlySequence<byte> input)
        {
            if (input.IsSingleSegment && MemoryMarshal.TryGetArray(input.First, out var segment))
            {
                return segment;
            }

            // Should be rare
            var array = input.ToArray();
            return new ArraySegment<byte>(array);
        }

        private static bool TryReadBodyLength(ref ReadOnlySequence<byte> input, out int headerLength, out int bodyLength)
        {
            var valueSequence = input.Slice(0, Math.Min(5, input.Length));
            if (valueSequence.IsSingleSegment)
            {
                var valueSpan =
#if NET5_0_OR_GREATER
                    valueSequence.FirstSpan;
#else
                    valueSequence.First.Span;
#endif
                return TryReadBodyLength(valueSpan, out headerLength, out bodyLength);
            }
            else
            {
                Span<byte> valueSpan = stackalloc byte[8];
                valueSequence.CopyTo(valueSpan);
                valueSpan = valueSpan[..(int) valueSequence.Length];
                return TryReadBodyLength(valueSpan, out headerLength, out bodyLength);
            }
        }

        private static bool TryReadBodyLength(ReadOnlySpan<byte> span, out int headerLength, out int bodyLength)
        {
            // Alorithm taken from https://docs.oasis-open.org/mqtt/mqtt/v3.1.1/errata01/os/mqtt-v3.1.1-errata01-os-complete.html.
            var multiplier = 1;
            var value = 0;
            byte encodedByte;
            var index = 1;
            headerLength = 0;
            bodyLength = 0;

            do
            {
                if (index == span.Length)
                {
                    return false;
                }

                encodedByte = span[index];
                index++;

                value += (byte) (encodedByte & 127) * multiplier;
                if (multiplier > 128 * 128 * 128)
                {
                    ThrowProtocolViolationException(span, index);
                }

                multiplier *= 128;
            } while ((encodedByte & 128) != 0);

            headerLength = index;
            bodyLength = value;
            return true;
        }

        private static void ThrowProtocolViolationException(ReadOnlySpan<byte> valueSpan, int index)
        {
            throw new MqttProtocolViolationException($"Remaining length is invalid (Data={string.Join(",", valueSpan.Slice(1, index).ToArray())}).");
        }
    }
}
