using System;

namespace BinarySerializer
{
    public static class PointerExtensions {

        public static Pointer<T>[] ResolveValue<T>(this Pointer<T>[] ptrs, SerializerObject s, PointerFunctions.SerializeFunction<T> func) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveValue(s, func);
            return ptrs;
        }
        public static Pointer<T>[] ResolveValue<T>(this Pointer<T>[] ptrs, SerializerObject s, Func<int, PointerFunctions.SerializeFunction<T>> func) {
            if (ptrs == null) return null;
            for (int i = 0; i < ptrs.Length; i++) {
                ptrs[i]?.ResolveValue(s, func(i));
            }
            return ptrs;
        }
        // Object
        public static Pointer<T> ResolveObject<T>(this Pointer<T> ptr, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.ResolveValue(s, PointerFunctions.SerializeObject<T>(onPreSerialize: onPreSerialize));
        }
        public static Pointer<T>[] ResolveObject<T>(this Pointer<T>[] ptrs, SerializerObject s, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            if (ptrs == null) return null;
            for (int i = 0; i < ptrs.Length; i++) {
                ptrs[i]?.ResolveObject(s, onPreSerialize: onPreSerialize != null ? x => onPreSerialize(x, i) : null);
            }
            return ptrs;
        }

        // Object array
        public static Pointer<T[]> ResolveObjectArray<T>(this Pointer<T[]> ptr, SerializerObject s, long count, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.ResolveValue(s, PointerFunctions.SerializeObjectArray<T>(count, onPreSerialize: onPreSerialize));
        }
        public static Pointer<T[]> ResolveObjectArrayUntil<T>(this Pointer<T[]> ptr, SerializerObject s, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            return ptr.ResolveValue(s, PointerFunctions.SerializeObjectArrayUntil<T>(conditionCheckFunc, getLastObjFunc: getLastObjFunc, onPreSerialize: onPreSerialize));
        }

        // Value
        public static Pointer<T> Resolve<T>(this Pointer<T> ptr, SerializerObject s) {
            return ptr.ResolveValue(s, PointerFunctions.Serialize<T>());
        }
        public static Pointer<T>[] Resolve<T>(this Pointer<T>[] ptrs, SerializerObject s) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.Resolve(s);
            return ptrs;
        }
        // Value array
        public static Pointer<T[]> ResolveArray<T>(this Pointer<T[]> ptr, SerializerObject s, long count) {
            return ptr.ResolveValue(s, PointerFunctions.SerializeArray<T>(count));
        }
        public static Pointer<T[]>[] ResolveArray<T>(this Pointer<T[]>[] ptrs, SerializerObject s, long count) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveArray(s, count);
            return ptrs;
        }

        // String
        public static Pointer<string> ResolveString(this Pointer<string> ptr, SerializerObject s, long? length = null, System.Text.Encoding encoding = null) {
            return ptr.ResolveValue(s, PointerFunctions.SerializeString(length: length, encoding: encoding));
        }
        public static Pointer<string>[] ResolveString(this Pointer<string>[] ptrs, SerializerObject s, long? length = null, System.Text.Encoding encoding = null) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveString(s, length: length, encoding: encoding);
            return ptrs;
        }

        // Pointer
        public static Pointer<Pointer> ResolvePointer(this Pointer<Pointer> ptr, SerializerObject s, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            return ptr.ResolveValue(s, PointerFunctions.SerializePointer(size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
        }
        public static Pointer<Pointer<T>> ResolvePointer<T>(this Pointer<Pointer<T>> ptr, SerializerObject s,  PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            return ptr.ResolveValue(s, PointerFunctions.SerializePointer<T>(size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
        }

        public static Pointer<Pointer[]> ResolvePointerArray(this Pointer<Pointer[]> ptr, SerializerObject s, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            return ptr.ResolveValue(s, PointerFunctions.SerializePointerArray(count, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
        }
        public static Pointer<Pointer<T>[]> ResolvePointerArray<T>(this Pointer<Pointer<T>[]> ptr, SerializerObject s, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            return ptr.ResolveValue(s, PointerFunctions.SerializePointerArray<T>(count, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
        }
    }
}