using System;

namespace BinarySerializer
{
    public class Pointer<T>
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

        public Pointer<T> ResolveValue(SerializerObject s, PointerFunctions.SerializeFunction<T> func) {
            if (s == null) 
                throw new ArgumentNullException(nameof(s));

            Context = s.Context;

            if (PointerValue == null) 
                return this;

            s.DoAt(PointerValue, () => {
                Value = func(s, Value, name: nameof(Value));
            });

            return this;
        }

        public static implicit operator T(Pointer<T> a) => a.Value;
        public static implicit operator Pointer(Pointer<T> a) => a?.PointerValue;

        public static Pointer<T> FromObject<T>(T obj) where T : BinarySerializable, new() {
            if(obj == null) return new Pointer<T>();
            return new Pointer<T>(obj?.Offset, value: obj);
        }
    }
}