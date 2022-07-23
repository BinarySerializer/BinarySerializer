namespace BinarySerializer
{
    public abstract class BitSerializerObject 
    {
        protected BitSerializerObject(SerializerObject serializerObject, Pointer valueOffset, string logPrefix, long value) 
        {
            SerializerObject = serializerObject;
            ValueOffset = valueOffset;
            LogPrefix = logPrefix;
            Value = value;
            Position = 0;
        }

        public SerializerObject SerializerObject { get; }
        public Context Context => SerializerObject.Context;
        public Pointer ValueOffset { get; }
        protected string LogPrefix { get; }
        public long Value { get; set; }
        public int Position { get; set; }

        public abstract T SerializeBits<T>(T value, int length, SignedNumberRepresentation sign = SignedNumberRepresentation.Unsigned, string name = null);

        public void SerializePadding(int length, bool logIfNotNull = false, string name = "Padding")
        {
            int pos = Position;

            long v = SerializeBits<long>(default, length, name: name);

            if (logIfNotNull && v != 0)
                Context.Logger?.LogWarning("Padding at {0} (bit position {1}) contains data! Data: 0x{2:X8}", ValueOffset, pos, v);
        }
    }
}
