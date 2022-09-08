#nullable enable
using System;
using System.IO;

namespace BinarySerializer
{
    /// <summary>
    /// An encoder wrapper for <see cref="IXORCalculator"/>. It recommended using
    /// <see cref="SerializerObject.DoXOR(IXORCalculator,System.Action)"/> where possible rather than this.
    /// </summary>
    public class XOREncoder : IStreamEncoder
    {
        public XOREncoder(IXORCalculator xorCalculator, long length)
        {
            XORCalculator = xorCalculator ?? throw new ArgumentNullException(nameof(xorCalculator));
            Length = length;
        }

        public IXORCalculator XORCalculator { get; }
        public long Length { get; }

        public string Name => "XOR";

        public void DecodeStream(Stream input, Stream output)
        {
            if (input == null) 
                throw new ArgumentNullException(nameof(input));
            if (output == null) 
                throw new ArgumentNullException(nameof(output));
            
            byte[] buffer = new byte[Length];
            input.Read(buffer, 0, buffer.Length);

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = XORCalculator.XORByte(buffer[i]);

            output.Write(buffer, 0, buffer.Length);
        }

        public void EncodeStream(Stream input, Stream output) => DecodeStream(input, output);
    }
}