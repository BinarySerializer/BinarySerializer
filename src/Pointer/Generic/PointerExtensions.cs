using System;

namespace BinarySerializer
{
    public static class PointerExtensions {
        // Object
        public static Pointer<T> ResolveObject<T>(this Pointer<T> ptr, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.Resolve(s, PointerFunctions.SerializeObject<T>(onPreSerialize: onPreSerialize));
        }
        public static Pointer<T>[] ResolveObject<T>(this Pointer<T>[] ptrs, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveObject(s, onPreSerialize: onPreSerialize);
            return ptrs;
        }

        // Object array
        public static Pointer<T[]> ResolveObjectArray<T>(this Pointer<T[]> ptr, SerializerObject s, long count, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.Resolve(s, PointerFunctions.SerializeObjectArray<T>(count, onPreSerialize: onPreSerialize));
        }
        public static Pointer<T[]> ResolveObjectArrayUntil<T>(this Pointer<T[]> ptr, SerializerObject s, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.Resolve(s, PointerFunctions.SerializeObjectArrayUntil<T>(conditionCheckFunc, getLastObjFunc: getLastObjFunc, onPreSerialize: onPreSerialize));
        }

        // Value
        public static Pointer<T> ResolveValue<T>(this Pointer<T> ptr, SerializerObject s) {
            return ptr.Resolve(s, PointerFunctions.Serialize<T>());
        }
        public static Pointer<T>[] ResolveValue<T>(this Pointer<T>[] ptrs, SerializerObject s) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveValue(s);
            return ptrs;
        }
        // Value array
        public static Pointer<T[]> ResolveValueArray<T>(this Pointer<T[]> ptr, SerializerObject s, long count) {
            return ptr.Resolve(s, PointerFunctions.SerializeArray<T>(count));
        }

        // String
        public static Pointer<string> ResolveString(this Pointer<string> ptr, SerializerObject s, long? length = null, System.Text.Encoding encoding = null) {
            return ptr.Resolve(s, PointerFunctions.SerializeString<string>(length: length, encoding: encoding));
        }
    }
}