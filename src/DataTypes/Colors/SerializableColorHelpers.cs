using System;
using System.Runtime.CompilerServices;

namespace BinarySerializer
{
    public static class SerializableColorHelpers
    {
        // Math.Pow(2, i) - 1
        private static readonly byte[] _byteFactors =
        {
            0,
            1, 3, 7, 15, 31, 63, 127, 255,
        };
        private static readonly float[] _floatFactors =
        {
            0f,
            1f, 3f, 7f, 15f, 31f, 63f, 127f, 255f,
            511f, 1023f, 2047f, 4095f, 8191f, 16383f, 32767f, 65535f,
            131071f, 262143f, 524287f, 1048575f, 2097151f, 4194303f, 8388607f, 16777215f,
            33554431f, 67108863f, 134217727f, 268435455f, 536870911f, 1073741823f, 2147483647f, 4294967295f,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static byte ToByte(float value, int bitsCount)
        {
            return (byte)Math.Round(value * _floatFactors[bitsCount]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ToFloat(byte value, int bitsCount)
        {
            return (value & _byteFactors[bitsCount]) / _floatFactors[bitsCount];
        }
    }
}