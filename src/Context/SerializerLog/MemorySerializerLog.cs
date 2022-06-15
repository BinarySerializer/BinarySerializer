using System;
using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// A serializer log which logs to memory, allowing the entire log to be retrieved as a string. This is not recommended
    /// for larger serializations as it can use a lot of memory.
    /// </summary>
    public class MemorySerializerLog : ISerializerLog
    {
        public bool IsEnabled { get; set; } = true;

        private StringBuilder _stringBuilder = new StringBuilder();

        public string GetString() => _stringBuilder?.ToString();
        public void Clear() => _stringBuilder?.Clear();
        public void Log(object obj) => _stringBuilder?.AppendLine(obj != null ? obj.ToString() : String.Empty);
        public void Dispose() => _stringBuilder = null;
    }
}