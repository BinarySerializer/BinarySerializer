using System;

namespace BinarySerializer
{
    public class Pointer<T> : IPointer<T>
    {
        public Pointer(Pointer pointerValue, T value = default)
        {
            Context = pointerValue?.Context;
            PointerValue = pointerValue;
            Value = value;
        }
        public Pointer()
        {
            PointerValue = null;
            Value = default;
        }

        public Context Context { get; private set; }
        public Pointer PointerValue { get; }
        public T Value { get; set; }

        public void ResolveValue(SerializerObject s, PointerFunctions.SerializeFunction<T> func) 
        {
            if (s == null) 
                throw new ArgumentNullException(nameof(s));
            if (func == null) 
                throw new ArgumentNullException(nameof(func));

            Context = s.Context;

            if (PointerValue == null) 
                return;

            s.DoAt(PointerValue, () => Value = func(s, Value, name: nameof(Value)));
        }

        public static implicit operator T(Pointer<T> a) => a.Value;
        public static implicit operator Pointer(Pointer<T> a) => a?.PointerValue;
    }
}