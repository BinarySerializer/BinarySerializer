using System;

namespace BinarySerializer
{
    public readonly struct Q8_8 : ISerializerShortLog
    {
        private Q8_8(short rawValue) => RawValue = rawValue;

        public const int Scale = 1 << 8;

        public short RawValue { get; }

        public float ToFloat() => RawValue / (float)Scale;

        public static implicit operator float(Q8_8 value) => value.ToFloat();
        public static implicit operator Q8_8(float value) => FromFloat(value);

        public static Q8_8 FromRaw(short value) => new(value);
#if NET
        public static Q8_8 FromFloat(float value) => new((short)MathF.Round(value * Scale));
#else 
        public static Q8_8 FromFloat(float value) => new((short)Math.Round(value * Scale));
#endif

        public static readonly SerializeInto<Q8_8> SerializeInto = (s, x) =>
        {
            short value = s.Serialize<short>(x.RawValue, name: nameof(RawValue));
            return new Q8_8(value);
        };

        public string ShortLog => ToString();
        public override string ToString() => $"{RawValue} ({ToFloat()})";
    }
}