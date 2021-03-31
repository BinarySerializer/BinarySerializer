namespace BinarySerializer
{
    /// <summary>
    /// Array of serializable objects. Mainly for files that are just simple object arrays. Use <see cref="SerializerObject.SerializeObjectArray"/> when possible
    /// </summary>
    /// <typeparam name="T">Generic parameter, should be a <see cref="BinarySerializable"/></typeparam>
    public class ObjectArray<T> : BinarySerializable where T : BinarySerializable, new() 
    {
        public long Length { get; set; } = 0;
        public T[] Value { get; set; }

        public override void SerializeImpl(SerializerObject s) {
			Value = s.SerializeObjectArray<T>(Value, Length, name: "Value");
		}
	}
}