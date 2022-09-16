#nullable enable
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

        private StringBuilder? _stringBuilder;
        protected StringBuilder StringBuilder => _stringBuilder ??= new StringBuilder();

        public string GetString() => StringBuilder.ToString();
        public void Clear() => StringBuilder.Clear();
        public void Write(object? obj) => StringBuilder.Append(obj);
        public void WriteLine(object? obj) => StringBuilder.AppendLine(obj?.ToString());
        public void Dispose() => _stringBuilder = null;
    }
}