#nullable enable
using System;

namespace BinarySerializer
{
    public interface ISerializerLog : IDisposable
    {
        bool IsEnabled { get; }
        void Write(object? obj);
        void WriteLine(object? obj);
    }

    // Temporary
    public static class SerializerLogExtensions
    {
        public static void Log(this ISerializerLog logger, object? obj) => logger.WriteLine(obj);
    }
}