#nullable enable
namespace BinarySerializer
{
    public struct CachedString
    {
        public CachedString(string stringValue, int serializedSize)
        {
            StringValue = stringValue;
            SerializedSize = serializedSize;
        }

        public string StringValue { get; }
        public int SerializedSize { get; }
    }
}