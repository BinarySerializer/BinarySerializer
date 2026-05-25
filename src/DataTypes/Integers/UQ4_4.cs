using System;

namespace BinarySerializer
{
    public readonly struct UQ4_4 : ISerializerShortLog
    {
        private UQ4_4(byte rawValue) => RawValue = rawValue;

        public const int Scale = 1 << 4;

        public byte RawValue { get; }

        public float ToFloat() => RawValue / (float)Scale;

        public static implicit operator float(UQ4_4 value) => value.ToFloat();
        public static implicit operator UQ4_4(float value) => FromFloat(value);

        public static UQ4_4 FromRaw(byte value) => new(value);
#if NET
        public static UQ4_4 FromFloat(float value) => new((byte)MathF.Round(value * Scale));
#else 
        public static UQ4_4 FromFloat(float value) => new((byte)Math.Round(value * Scale));
#endif

        public static SerializeInto<UQ4_4> SerializeInto = (s, x) =>
        {
            byte value = s.Serialize<byte>(x.RawValue, name: nameof(RawValue));
            return new UQ4_4(value);
        };

        public string ShortLog => ToString();
        public override string ToString() => $"{RawValue} ({ToFloat()})";
    }
}