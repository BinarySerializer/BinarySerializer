using System;

namespace BinarySerializer
{
    public interface ISerializerLog : IDisposable
    {
        bool IsEnabled { get; }
        string OverrideLogPath { get; set; }
        void Log(object obj);
    }
}