namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding RGB-666
    /// </summary>
    public class RGB666Color : BaseBytewiseRGBColor
    {
        public RGB666Color() { }
        public RGB666Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }

        public override float Red
        {
            get => R / 63f;
            set => R = (byte)(value * 63);
        }
        public override float Green
        {
            get => G / 63f;
            set => G = (byte)(value * 63);
        }
        public override float Blue
        {
            get => B / 63f;
            set => B = (byte)(value * 63);
        }
        public override float Alpha
        {
            get => 1f;
            set => _ = value;
        }

        public override void SerializeImpl(SerializerObject s)
        {
            R = s.Serialize<byte>(R, name: nameof(R));
            G = s.Serialize<byte>(G, name: nameof(G));
            B = s.Serialize<byte>(B, name: nameof(B));
        }
    }
}