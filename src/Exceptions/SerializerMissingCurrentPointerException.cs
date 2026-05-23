#nullable enable
using System;

[Serializable]
public class SerializerMissingCurrentPointerException : Exception
{
    public SerializerMissingCurrentPointerException() 
        : this("The serializer does not have a current pointer defined. To use the serializer you must first call GoTo() to set the current pointer from where to serialize.") 
    { }
    public SerializerMissingCurrentPointerException(string message) : base(message) { }
    public SerializerMissingCurrentPointerException(string message, Exception inner) : base(message, inner) { }
}