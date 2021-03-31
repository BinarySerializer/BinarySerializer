namespace BinarySerializer
{
    public class DefaultSerializerLog : ISerializerLog
    {
        public bool IsEnabled => false;
        public string OverrideLogPath { get; set; }
        public void Log(object obj) { }

        public void WriteLog() { }
    }
}