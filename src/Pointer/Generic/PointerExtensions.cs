#nullable enable
using System;
using System.Text;

namespace BinarySerializer
{
    public static class PointerExtensions
    {
        #region Main

        public static Pointer<T>[] ResolveValue<T>(
            this Pointer<T>?[] ptrs,
            SerializerObject s,
            SerializeFunction<T> func)
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T>();
                else
                    ptr.ResolveValue(s, func);
            }

            return ptrs!;
        }
        public static Pointer<T>[] ResolveValue<T>(
            this Pointer<T>?[] ptrs,
            SerializerObject s,
            Func<int, SerializeFunction<T>> func)
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T>();
                else
                    ptr.ResolveValue(s, func(i));
            }

            return ptrs!;
        }

        #endregion

        #region Serialization

        // Value
        public static Pointer<T> Resolve<T>(this Pointer<T> ptr, SerializerObject s)
            where T : struct
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.Serialize<T>());
        }
        public static Pointer<T>[] Resolve<T>(this Pointer<T>?[] ptrs, SerializerObject s)
            where T : struct
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T>();
                else
                    ptr.Resolve(s);
            }

            return ptrs!;
        }

        // Nullable value
        public static Pointer<T?> ResolveNullable<T>(this Pointer<T?> ptr, SerializerObject s)
            where T : struct
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeNullable<T>());
        }
        public static Pointer<T?>[] ResolveNullable<T>(this Pointer<T?>?[] ptrs, SerializerObject s)
            where T : struct
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T?>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T?>();
                else
                    ptr.ResolveNullable(s);
            }

            return ptrs!;
        }

        // Object
        public static Pointer<T> ResolveObject<T>(this Pointer<T> ptr, SerializerObject s, Action<T>? onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeObject<T>(onPreSerialize: onPreSerialize));
        }
        public static Pointer<T>[] ResolveObject<T>(this Pointer<T>?[] ptrs, SerializerObject s, Action<T, int>? onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T>();
                else
                    // ReSharper disable once AccessToModifiedClosure
                    ptr.ResolveObject(s, onPreSerialize: onPreSerialize != null ? x => onPreSerialize(x, i) : null);
            }

            return ptrs!;
        }

        // Pointer
        public static Pointer<Pointer> ResolvePointer(
            this Pointer<Pointer> ptr,
            SerializerObject s,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializePointer(
                size: size,
                anchor: anchor,
                allowInvalid: allowInvalid,
                nullValue: nullValue));
        }
        public static Pointer<Pointer<T>> ResolvePointer<T>(
            this Pointer<Pointer<T>> ptr,
            SerializerObject s,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializePointer<T>(
                size: size,
                anchor: anchor,
                allowInvalid: allowInvalid,
                nullValue: nullValue));
        }

        // String
        public static Pointer<string> ResolveString(
            this Pointer<string> ptr,
            SerializerObject s,
            long? length = null,
            Encoding? encoding = null)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeString(length: length, encoding: encoding));
        }
        public static Pointer<string>[] ResolveString(
            this Pointer<string>?[] ptrs,
            SerializerObject s,
            long? length = null,
            Encoding? encoding = null)
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<string>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<string>();
                else
                    ptr.ResolveString(s, length: length, encoding: encoding);
            }

            return ptrs!;
        }

        #endregion

        #region Array Serialization

        public static Pointer<T[]> ResolveArray<T>(this Pointer<T[]> ptr, SerializerObject s, long count)
            where T : struct
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeArray<T>(count));
        }
        public static Pointer<T[]>[] ResolveArray<T>(this Pointer<T[]>?[] ptrs, SerializerObject s, long count)
            where T : struct
        {
            if (ptrs == null)
                throw new ArgumentNullException(nameof(ptrs));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            for (int i = 0; i < ptrs.Length; i++)
            {
                Pointer<T[]>? ptr = ptrs[i];

                if (ptr == null)
                    ptrs[i] = new Pointer<T[]>();
                else
                    ptr.ResolveArray(s, count);
            }

            return ptrs!;
        }

        public static Pointer<T[]> ResolveObjectArray<T>(
            this Pointer<T[]> ptr,
            SerializerObject s,
            long count,
            Action<T, int>? onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeObjectArray<T>(count, onPreSerialize: onPreSerialize));
        }

        public static Pointer<Pointer?[]> ResolvePointerArray(
            this Pointer<Pointer?[]> ptr,
            SerializerObject s,
            long count,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializePointerArray(
                count: count,
                size: size,
                anchor: anchor,
                allowInvalid: allowInvalid,
                nullValue: nullValue));
        }

        public static Pointer<Pointer<T>[]> ResolvePointerArray<T>(
            this Pointer<Pointer<T>[]> ptr,
            SerializerObject s,
            long count,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null)
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializePointerArray<T>(
                count: count,
                size: size,
                anchor: anchor,
                allowInvalid: allowInvalid,
                nullValue: nullValue));
        }

        public static Pointer<T[]> ResolveObjectArrayUntil<T>(
            this Pointer<T[]> ptr,
            SerializerObject s,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            Action<T, int>? onPreSerialize = null)
            where T : BinarySerializable, new()
        {
            if (ptr == null)
                throw new ArgumentNullException(nameof(ptr));
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            return ptr.ResolveValue(s, PointerFunctions.SerializeObjectArrayUntil<T>(
                conditionCheckFunc: conditionCheckFunc,
                getLastObjFunc: getLastObjFunc,
                onPreSerialize: onPreSerialize));
        }

        #endregion
    }
}