#nullable enable
using JetBrains.Annotations;

namespace BinarySerializer
{
    public static class LoggerExtensions
    {
        [StringFormatMethod("log")]
        public static void LogTrace(this ILogger logger, object? log, params object[] args) =>
            logger.Log(LogLevel.Trace, log, args);

        [StringFormatMethod("log")]
        public static void LogDebug(this ILogger logger, object? log, params object[] args) =>
            logger.Log(LogLevel.Debug, log, args);

        [StringFormatMethod("log")]
        public static void LogInfo(this ILogger logger, object? log, params object[] args) =>
            logger.Log(LogLevel.Info, log, args);

        [StringFormatMethod("log")]
        public static void LogWarning(this ILogger logger, object? log, params object[] args) =>
            logger.Log(LogLevel.Warning, log, args);

        [StringFormatMethod("log")]
        public static void LogError(this ILogger logger, object? log, params object[] args) =>
            logger.Log(LogLevel.Error, log, args);
    }
}