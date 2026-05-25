#nullable enable
using System;
using System.Collections.Generic;

namespace BinarySerializer
{
    public class SerializableCache 
    {
        public SerializableCache(ISystemLogger? systemLogger)
        {
            SystemLogger = systemLogger;
            Objects = new Dictionary<Type, Dictionary<Pointer, BinarySerializable>>();
            Strings = new Dictionary<Pointer, CachedString>();
        }

        private ISystemLogger? SystemLogger { get; }
        private Dictionary<Type, Dictionary<Pointer, BinarySerializable>> Objects { get; }
        private Dictionary<Pointer, CachedString> Strings { get; }

        public T? GetObject<T>(Pointer? pointer) 
            where T : BinarySerializable 
        {
            if (pointer == null) 
                return null;

            Type type = typeof(T);

            if (Objects.TryGetValue(type, out Dictionary<Pointer, BinarySerializable>? dict) &&
                dict.TryGetValue(pointer, out BinarySerializable? obj))
                return obj as T;
            else
                return null;
        }

        public CachedString? GetString(Pointer? pointer)
        {
            if (pointer == null) 
                return null;

            if (Strings.TryGetValue(pointer, out CachedString value))
                return value;
            else
                return null;
        }

        public void AddObject<T>(T serializable) 
            where T : BinarySerializable 
        {
            if (serializable == null) 
                throw new ArgumentNullException(nameof(serializable));
            
            Pointer pointer = serializable.Offset;
            Type type = typeof(T);

            if (!Objects.ContainsKey(type))
                Objects[type] = new Dictionary<Pointer, BinarySerializable>();

            if (!Objects[type].ContainsKey(pointer)) 
                Objects[type][pointer] = serializable;
            else 
                SystemLogger?.LogWarning("Duplicate pointer {0} for type {1}", pointer, type);
        }

        public void AddString(Pointer pointer, CachedString value)
        {
            if (pointer == null) 
                throw new ArgumentNullException(nameof(pointer));

            if (!Strings.ContainsKey(pointer))
                Strings[pointer] = value;
            else
                SystemLogger?.LogWarning("Duplicate pointer {0} for string", pointer);
        }

        public void ClearForFile(BinaryFile file)
        {
            List<Pointer> pointersToRemove = new();

            // Remove objects
            foreach (Dictionary<Pointer, BinarySerializable> objs in Objects.Values)
            {
                foreach (Pointer p in objs.Keys)
                {
                    if (p.File == file)
                        pointersToRemove.Add(p);
                }

                if (pointersToRemove.Count > 0)
                {
                    foreach (Pointer p in pointersToRemove)
                        objs.Remove(p);
                    pointersToRemove.Clear();
                }
            }

            // Remove strings
            foreach (Pointer p in Strings.Keys)
            {
                if (p.File == file)
                    pointersToRemove.Add(p);
            }
            foreach (Pointer p in pointersToRemove)
                Strings.Remove(p);
        }

        public void Clear()
        {
            Objects.Clear();
            Strings.Clear();
        }
    }
}