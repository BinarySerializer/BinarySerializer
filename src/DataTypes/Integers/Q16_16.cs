using System;

namespace BinarySerializer
{
    public readonly struct Q16_16 : ISerializerShortLog
    {
        private Q16_16(int rawValue) => RawValue = rawValue;

        public const int Scale = 1 << 16;

        public int RawValue { get; }

        public float ToFloat() => RawValue / (float)Scale;

        public static implicit operator float(Q16_16 value) => value.ToFloat();
        public static implicit operator Q16_16(float value) => FromFloat(value);

        public static Q16_16 FromRaw(int value) => new(value);
#if NET
        public static Q16_16 FromFloat(float value) => new((int)MathF.Round(value * Scale));
#else 
        public static Q16_16 FromFloat(float value) => new((int)Math.Round(value * Scale));
#endif

        public static readonly SerializeInto<Q16_16> SerializeInto = (s, x) =>
        {
            int value = s.Serialize<int>(x.RawValue, name: nameof(RawValue));
            return new Q16_16(value);
        };

        public string ShortLog => ToString();
        public override string ToString() => $"{RawValue} ({ToFloat()})";
    }
}