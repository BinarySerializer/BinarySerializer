#nullable enable
using JetBrains.Annotations;

namespace BinarySerializer
{
    public interface ILogger
    {
        [StringFormatMethod("log")]
        void Log(LogLevel logLevel, object? log, params object[] args);
    }
}