using System;

namespace BinarySerializer
{
    public class ArrayPointer<T> where T : BinarySerializable, new() 
    {
        public ArrayPointer(Pointer pointerValue, T[] value = default)
        {
            Context = pointerValue?.Context;
            PointerValue = pointerValue;
            Value = value;
        }
        public ArrayPointer()
        {
            PointerValue = null;
            Value = null;
        }

        public Context Context { get; private set; }
        public Pointer PointerValue { get; }
        public T[] Value { get; set; }

        public ArrayPointer<T> Resolve(SerializerObject s, long count, Action<T> onPreSerialize = null)
        {
            if (s == null) 
                throw new ArgumentNullException(nameof(s));

            Context = s.Context;
            
            if (PointerValue == null) 
                return this;

            s.DoAt(PointerValue, () => 
                Value = s.SerializeObjectArray<T>(Value, count, onPreSerialize: onPreSerialize, name: nameof(Value)));
            
            return this;
        }

        public ArrayPointer<T> ResolveUntil(SerializerObject s, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, Action<T, int> onPreSerialize = null) {
            if (s == null)
                throw new ArgumentNullException(nameof(s));

            Context = s.Context;

            if (PointerValue == null)
                return this;

            s.DoAt(PointerValue, () =>
                Value = s.SerializeObjectArrayUntil<T>(Value, conditionCheckFunc, getLastObjFunc: getLastObjFunc, onPreSerialize: onPreSerialize, name: nameof(Value)));

            return this;
        }

        public static implicit operator T[](ArrayPointer<T> a) => a?.Value;
        public static implicit operator ArrayPointer<T>(T[] t) => t == null ? new ArrayPointer<T>(null, null) : new ArrayPointer<T>(t[0].Offset, t);
        public static implicit operator Pointer(ArrayPointer<T> a) => a?.PointerValue;
    }
}