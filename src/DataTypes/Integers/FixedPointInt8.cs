namespace BinarySerializer
{
    public class FixedPointInt8 : BinarySerializable, ISerializerShortLog
    {
        // Set in onPreSerialize
        public int Pre_PointPosition { get; set; } = 4; // By default, the point will be at 4 bits

        public byte Value { get; set; }

        public float AsFloat 
        {
            get 
            {
                int divisor = 1 << Pre_PointPosition;
                float val = Value / (float)divisor;
                return val;
            }
            set 
            {
                int divisor = 1 << Pre_PointPosition;
                Value = (byte)(value * divisor);
            }
        }
        public static implicit operator float(FixedPointInt8 d) => d?.AsFloat ?? 0f;

        public static implicit operator FixedPointInt8(float f) => new FixedPointInt8() 
        {
            AsFloat = f
        };

        public override void SerializeImpl(SerializerObject s) 
        {
            Value = s.Serialize<byte>(Value, name: nameof(Value));
        }

        public string ShortLog => ToString();
        public override string ToString() => AsFloat.ToString();
    }
}