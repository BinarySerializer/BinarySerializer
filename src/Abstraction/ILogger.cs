namespace BinarySerializer
{
    public interface ILogger
    {
        void Log(object log);
        void LogWarning(object log);
        void LogError(object log);
    }
}