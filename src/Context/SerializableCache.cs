using System;
using System.Collections.Generic;

namespace BinarySerializer
{
    public class SerializableCache 
    {
        public SerializableCache(ISystemLog systemLog)
        {
            SystemLog = systemLog;
            Structs = new Dictionary<Type, Dictionary<Pointer, BinarySerializable>>();
        }

        protected ISystemLog SystemLog { get; }

        public Dictionary<Type, Dictionary<Pointer, BinarySerializable>> Structs { get; }

        public T FromOffset<T>(Pointer pointer) 
            where T : BinarySerializable 
        {
            if (pointer == null) 
                return null;

            Type type = typeof(T);

            if (!Structs.ContainsKey(type) || !Structs[type].ContainsKey(pointer)) 
                return null;

            return Structs[type][pointer] as T;
        }

        public void Add<T>(T serializable) 
            where T : BinarySerializable 
        {
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