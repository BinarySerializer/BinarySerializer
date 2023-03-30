#nullable enable

namespace BinarySerializer
{
    public delegate T SerializeInto<T>(SerializerObject s, T value);
}