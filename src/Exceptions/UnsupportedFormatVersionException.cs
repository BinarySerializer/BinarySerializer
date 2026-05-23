#nullable enable
using System;

namespace BinarySerializer
{
    [Serializable]
    public class UnsupportedFormatVersionException : BinarySerializableException
    {
        public UnsupportedFormatVersionException(BinarySerializable data) : base(data) { }
        public UnsupportedFormatVersionException(BinarySerializable data, string? message) : base(data, message) { }
        public UnsupportedFormatVersionException(BinarySerializable data, string? message, Exception inner) : base(data, message, inner) { }
    }
}