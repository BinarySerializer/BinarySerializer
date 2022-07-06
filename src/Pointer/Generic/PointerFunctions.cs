using System;

namespace BinarySerializer
{
    public static class PointerFunctions
    {
        public delegate T SerializeFunction<T>(SerializerObject s, T value, string name = null);

        public static SerializeFunction<T> SerializeObject<T>(Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            return (s, value, name) => s.SerializeObject<T>(value, onPreSerialize: onPreSerialize, name: name);
        }
        public static SerializeFunction<T[]> SerializeObjectArray<T>(long count, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return (s, value, name) => s.SerializeObjectArray<T>(value, count, onPreSerialize: onPreSerialize, name: name);
        }
        public static SerializeFunction<T[]> SerializeObjectArrayUntil<T>(Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return (s, value, name) => s.SerializeObjectArrayUntil<T>(value, conditionCheckFunc, getLastObjFunc: getLastObjFunc, onPreSerialize: onPreSerialize, name: name);
        }
        public static SerializeFunction<T> Serialize<T>() =>
            (s, value, name) => s.Serialize<T>(value, name: name);

        public static SerializeFunction<T[]> SerializeArray<T>(long count) =>
            (s, value, name) => s.SerializeArray<T>(value, count, name: name);

        public static SerializeFunction<T[]> SerializeArrayUntil<T>(Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null) =>
            (s, value, name) => s.SerializeArrayUntil<T>(value, conditionCheckFunc, getLastObjFunc: getLastObjFunc, name: name);

        public static SerializeFunction<T> SerializeString<T>(long? length = null, System.Text.Encoding encoding = null) =>
            (s, value, name) => (T)(object)s.SerializeString((string)(object)value, length: length, encoding: encoding, name: name);
    }
}