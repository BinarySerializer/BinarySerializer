namespace BinarySerializer
{
    public interface IPointer<T>
    {
        public T Value { get; set; }
        public void ResolveValue(SerializerObject s, PointerFunctions.SerializeFunction<T> func);
    }
}