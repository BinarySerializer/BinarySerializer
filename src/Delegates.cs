#nullable enable

namespace BinarySerializer
{
    public delegate T SerializeInto<T>(SerializerObject s, T value);

    public delegate T DoArrayAction<T>(T? item, string? name = null);
}