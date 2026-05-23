#nullable enable
using System;

namespace BinarySerializer
{
    [Serializable]
    public class UnsupportedDataTypeException : Exception
    {
        public UnsupportedDataTypeException() { }
        public UnsupportedDataTypeException(string? message) : base(message) { }
        public UnsupportedDataTypeException(string? message, Exception inner) : base(message, inner) { }
    }
}