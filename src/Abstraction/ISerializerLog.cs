namespace BinarySerializer
{
    public interface ISerializerLog
    {
        bool IsEnabled { get; }
        string OverrideLogPath { get; set; }
        void Log(object obj);
        void WriteLog();
    }
}