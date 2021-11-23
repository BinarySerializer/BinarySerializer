namespace BinarySerializer {
    public abstract class BitSerializerObject {
        protected BitSerializerObject(SerializerObject serializerObject, string logPrefix, long value) {
            SerializerObject = serializerObject;
            LogPrefix = logPrefix;
            Value = value;
            Position = 0;
        }

        public SerializerObject SerializerObject { get; }
        public Context Context => SerializerObject.Context;
        protected string LogPrefix { get; }
        public long Value { get; set; }
        public int Position { get; protected set; }

        public abstract T SerializeBits<T>(T value, int length, string name = null);

        // Other helpers can be added here
    }
}
