using System;

namespace BinarySerializer
{
    public static class PointerExtensions {

        public static IPointer<T>[] ResolveValue<T>(this IPointer<T>[] ptrs, SerializerObject s, PointerFunctions.SerializeFunction<T> func) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveValue(s, func);
            return ptrs;
        }
        public static IPointer<T>[] ResolveValue<T>(this IPointer<T>[] ptrs, SerializerObject s, Func<int, PointerFunctions.SerializeFunction<T>> func) {
            if (ptrs == null) return null;
            for (int i = 0; i < ptrs.Length; i++) {
                ptrs[i]?.ResolveValue(s, func(i));
            }
            return ptrs;
        }
        // Object
        public static IPointer<T> ResolveObject<T>(this IPointer<T> ptr, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            ptr.ResolveValue(s, PointerFunctions.SerializeObject<T>(onPreSerialize: onPreSerialize));
            return ptr;
        }
        public static IPointer<T>[] ResolveObject<T>(this IPointer<T>[] ptrs, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveObject(s, onPreSerialize: onPreSerialize);
            return ptrs;
        }

        // Object array
        public static IPointer<T[]> ResolveObjectArray<T>(this IPointer<T[]> ptr, SerializerObject s, long count, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            ptr.ResolveValue(s, PointerFunctions.SerializeObjectArray<T>(count, onPreSerialize: onPreSerialize));
            return ptr;
        }
        public static IPointer<T[]> ResolveObjectArrayUntil<T>(this IPointer<T[]> ptr, SerializerObject s, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null) where T : BinarySerializable, new() {
            ptr.ResolveValue(s, PointerFunctions.SerializeObjectArrayUntil<T>(conditionCheckFunc, getLastObjFunc: getLastObjFunc, onPreSerialize: onPreSerialize));
            return ptr;
        }

        // Value
        public static IPointer<T> Resolve<T>(this IPointer<T> ptr, SerializerObject s) {
            ptr.ResolveValue(s, PointerFunctions.Serialize<T>());
            return ptr;
        }
        public static IPointer<T>[] Resolve<T>(this IPointer<T>[] ptrs, SerializerObject s) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.Resolve(s);
            return ptrs;
        }
        // Value array
        public static IPointer<T[]> ResolveArray<T>(this IPointer<T[]> ptr, SerializerObject s, long count) {
            ptr.ResolveValue(s, PointerFunctions.SerializeArray<T>(count));
            return ptr;
        }
        public static IPointer<T[]>[] ResolveArray<T>(this IPointer<T[]>[] ptrs, SerializerObject s, long count) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveArray(s, count);
            return ptrs;
        }

        // String
        public static IPointer<string> ResolveString(this IPointer<string> ptr, SerializerObject s, long? length = null, System.Text.Encoding encoding = null) {
            ptr.ResolveValue(s, PointerFunctions.SerializeString(length: length, encoding: encoding));
            return ptr;
        }
        public static IPointer<string>[] ResolveString(this IPointer<string>[] ptrs, SerializerObject s, long? length = null, System.Text.Encoding encoding = null) {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.ResolveString(s, length: length, encoding: encoding);
            return ptrs;
        }

        // Pointer
        public static IPointer<Pointer> ResolvePointer(this IPointer<Pointer> ptr, SerializerObject s, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            ptr.ResolveValue(s, PointerFunctions.SerializePointer(size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
            return ptr;
        }
        public static IPointer<Pointer<T>> ResolveIPointer<T>(this IPointer<Pointer<T>> ptr, SerializerObject s, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            ptr.ResolveValue(s, PointerFunctions.SerializePointer<T>(size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
            return ptr;
        }

        public static IPointer<Pointer[]> ResolvePointerArray(this IPointer<Pointer[]> ptr, SerializerObject s, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            ptr.ResolveValue(s, PointerFunctions.SerializePointerArray(count, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
            return ptr;
        }
        public static IPointer<Pointer<T>[]> ResolvePointerArray<T>(this IPointer<Pointer<T>[]> ptr, SerializerObject s, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null) {
            ptr.ResolveValue(s, PointerFunctions.SerializePointerArray<T>(count, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue));
            return ptr;
        }
    }
}