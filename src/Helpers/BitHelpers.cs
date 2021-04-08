﻿namespace BinarySerializer
{
    /// <summary>
    /// Bit helper methods
    /// </summary>
    public static class BitHelpers
    {
        /// <summary>
        /// Extracts the bits from a value
        /// </summary>
        /// <param name="value">The value to extract the bits from</param>
        /// <param name="count">The amount of bits to extract</param>
        /// <param name="offset">The offset to start from</param>
        /// <returns>The extracted bits as an integer</returns>
        public static int ExtractBits(int value, int count, int offset)
        {
            return (((1 << count) - 1) & (value >> (offset)));
        }

        public static int SetBits(int bits, int value, int count, int offset) {
            int mask = ((1 << count) - 1) << offset;
            bits = (bits & ~mask) | (value << offset);
            return bits;
        }

        public static int ReverseBits(int value)
        {
            var result = 0;

            for (int i = 0; i < 32; i++)
                result = SetBits(result, ExtractBits(value, 1, i), 1, 32 - i - 1);

            return result;
        }

        public static void CopyBits(ref byte b1, ref byte b2, int count, int offset1, int offset2, bool setB1)
        {
            if (setB1)
                b1 = (byte)SetBits(b1, ExtractBits(b2, count, offset2), count, offset1);
            else
                b2 = (byte)SetBits(b2, ExtractBits(b1, count, offset1), count, offset2);
        }
    }
}