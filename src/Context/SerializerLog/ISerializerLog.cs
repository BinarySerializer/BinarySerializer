#nullable enable
using System;

namespace BinarySerializer
{
    public interface ISerializerLog : IDisposable
    {
        bool IsEnabled { get; }
        void Log(object? obj);
    }
}