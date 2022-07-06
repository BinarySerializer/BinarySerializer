using System;

namespace BinarySerializer
{
    public class Pointer<T> where T : BinarySerializable, new() 
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
            Value = null;
        }

        public Context Context { get; private set; }
        public Pointer PointerValue { get; }
        public T Value { get; set; }

        public Pointer<T> Resolve(SerializerObject s, Action<T> onPreSerialize = null)
        {
            if (s == null) 
                throw new ArgumentNullException(nameof(s));

            Context = s.Context;

            if (PointerValue == null) 
                return this;
            
            Value = PointerValue.Context.Cache.FromOffset<T>(PointerValue);
            s.DoAt(PointerValue, () => Value = s.SerializeObject<T>(Value, onPreSerialize: onPreSerialize, name: nameof(Value)));
            
            return this;
        }
        public Pointer<T> Resolve(Context c) 
        {
            if (c == null) 
                throw new ArgumentNullException(nameof(c));

            Context = c;

            if (PointerValue != null)
                Value = c.Cache.FromOffset<T>(PointerValue);

            return this;
        }

        public static implicit operator T(Pointer<T> a) => a?.Value;
        public static implicit operator Pointer<T>(T t) => t == null ? new Pointer<T>(null, null) : new Pointer<T>(t.Offset, t);
        public static implicit operator Pointer(Pointer<T> a) => a?.PointerValue;
    }
}