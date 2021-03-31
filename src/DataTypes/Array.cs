namespace BinarySerializer
{
    /// <summary>
    /// Array of simple non-R1Serializable types. Mainly for files that are just simple arrays. Use <see cref="SerializerObject.SerializeArray"/> where possible
    /// </summary>
    /// <typeparam name="T">Generic value parameter, should not be <see cref="BinarySerializable"/></typeparam>
    public class Array<T> : BinarySerializable 
    {
		public long Length { get; set; } = 0;
		public T[] Value { get; set; }

		public override void SerializeImpl(SerializerObject s) 
        {
			Value = s.SerializeArray<T>(Value, Length, name: "Value");
		}
	}
}