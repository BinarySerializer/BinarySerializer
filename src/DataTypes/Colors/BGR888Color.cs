namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding BGR-888
    /// </summary>
    public class BGR888Color : BaseColor
    {
        public BGR888Color() { }
        public BGR888Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }

        public byte R { get; set; }
        public byte G { get; set; }
        public byte B { get; set; }

        public override float Red
        {
            get => R / 255f;
            set => R = (byte)(value * 255);
        }
        public override float Green
        {
            get => G / 255f;
            set => G = (byte)(value * 255);
        }
        public override float Blue
        {
            get => B / 255f;
            set => B = (byte)(value * 255);
        }
        public override float Alpha
        {
            get => 1f;
            set => _ = value;
        }

        public override void SerializeImpl(SerializerObject s)
        {
            B = s.Serialize<byte>(B, name: nameof(B));
            G = s.Serialize<byte>(G, name: nameof(G));
            R = s.Serialize<byte>(R, name: nameof(R));
        }
    }
}