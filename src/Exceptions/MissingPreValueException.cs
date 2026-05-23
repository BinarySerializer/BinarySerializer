#nullable enable
using System;

namespace BinarySerializer
{
    [Serializable]
    public class MissingPreValueException : BinarySerializableException
    {
        public MissingPreValueException(BinarySerializable data, string? preValueName) 
            : base(data, $"Missing required pre-value {preValueName}")
        {
            PreValueName = preValueName;
        }
        public MissingPreValueException(BinarySerializable data, string? preValueName, string message) 
            : base(data, message)
        {
            PreValueName = preValueName;
        }

        public MissingPreValueException(BinarySerializable data, string? preValueName, string message, Exception inner)
            : base(data, message, inner)
        {
            PreValueName = preValueName;
        }

        public string? PreValueName { get; }
    }
}