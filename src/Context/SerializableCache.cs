#nullable enable
using System;
using System.Collections.Generic;

namespace BinarySerializer
{
    public class SerializableCache 
    {
        public SerializableCache(ISystemLog? systemLog)
        {
            SystemLog = systemLog;
            Structs = new Dictionary<Type, Dictionary<Pointer, BinarySerializable>>();
        }

        protected ISystemLog? SystemLog { get; }

        // TODO: Optimize this by using single dictionary with some key being hash of type and pointer?
        public Dictionary<Type, Dictionary<Pointer, BinarySerializable>> Structs { get; }

        public T? FromOffset<T>(Pointer? pointer) 
            where T : BinarySerializable 
        {
            if (pointer == null) 
                return null;

            Type type = typeof(T);

            if (Structs.TryGetValue(type, out Dictionary<Pointer, BinarySerializable> dict) &&
                dict.TryGetValue(pointer, out BinarySerializable obj))
                return obj as T;
            else
                return null;
        }

        public void Add<T>(T serializable) 
            where T : BinarySerializable 
        {
            if (serializable == null) 
                throw new ArgumentNullException(nameof(serializable));
            
            Pointer pointer = serializable.Offset;
            Type type = typeof(T);

            if (!Structs.ContainsKey(type))
                Structs[type] = new Dictionary<Pointer, BinarySerializable>();

            if (!Structs[type].ContainsKey(pointer)) 
                Structs[type][pointer] = serializable;
            else 
                SystemLog?.LogWarning("Duplicate pointer {0} for type {1}", pointer, type);
        }

        public void Clear() => Structs.Clear();
    }
}