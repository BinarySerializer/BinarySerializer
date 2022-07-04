using System;

namespace BinarySerializer
{
    public static class PointerExtensions {
        public static Pointer<T>[] Resolve<T>(this Pointer<T>[] ptrs, SerializerObject s, Action<T> onPreSerialize = null) where T : BinarySerializable, new() {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.Resolve(s, onPreSerialize: onPreSerialize);
            return ptrs;
        }
        public static Pointer<T>[] Resolve<T>(this Pointer<T>[] ptrs, Context c) where T : BinarySerializable, new() {
            if (ptrs == null) return null;
            foreach (var ptr in ptrs)
                ptr?.Resolve(c);
            return ptrs;
        }
    }
}