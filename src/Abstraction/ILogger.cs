using JetBrains.Annotations;

namespace BinarySerializer
{
    public interface ILogger
    {
        [StringFormatMethod("log")]
        void Log(object log, params object[] args);

        [StringFormatMethod("log")]
        void LogWarning(object log, params object[] args);

        [StringFormatMethod("log")]
        void LogError(object log, params object[] args);
    }
}