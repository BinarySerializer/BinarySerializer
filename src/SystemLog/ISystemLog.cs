#nullable enable
using JetBrains.Annotations;

namespace BinarySerializer
{
    public interface ISystemLog
    {
        [StringFormatMethod("log")]
        void Log(LogLevel logLevel, object? log, params object?[] args);
    }
}