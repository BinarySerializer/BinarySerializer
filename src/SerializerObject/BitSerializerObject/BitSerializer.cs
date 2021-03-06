using System;

namespace BinarySerializer 
{
    public class BitSerializer : BitSerializerObject 
    {
        public BitSerializer(SerializerObject serializerObject, Pointer valueOffset, string logPrefix, long value) 
            : base(serializerObject, valueOffset, logPrefix, value) { }

        public override T SerializeBits<T>(T value, int length, SignedNumberRepresentation sign = SignedNumberRepresentation.Unsigned, string name = null) 
        {
            long valueToWrite = ObjectToLong<T>(value);
            Value = BitHelpers.SetBits64(Value, valueToWrite, length, Position, sign: sign);

            if (SerializerObject.IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}  {Position}_{length} ({typeof(T).Name}) {name ?? "<no name>"}: {valueToWrite}");

            Position += length;

            return value;
        }

        protected long ObjectToLong<T>(T value) 
        {
            if (value?.GetType().IsEnum == true)
                return ObjectToLong(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())));

            else if (value is bool bo)
                return bo ? 1 : 0;

            else if (value is sbyte sb)
                return sb;

            else if (value is byte by)
                return by;

            else if (value is short sh)
                return sh;

            else if (value is ushort ush)
                return ush;

            else if (value is int i32)
                return i32;

            else if (value is uint ui32)
                return ui32;

            else if (value is long lo)
                return lo;

            else if (value is ulong ulo)
                return BitConverter.ToInt64(BitConverter.GetBytes(ulo), 0);

            else if (value is float fl)
                return BitConverter.ToInt64(BitConverter.GetBytes(fl), 0);

            else if (value is double dou)
                return BitConverter.ToInt64(BitConverter.GetBytes(dou), 0);

            else if (value is UInt24 u24)
                return u24.Value;

            else if (Nullable.GetUnderlyingType(typeof(T)) != null) 
            {
                // It's nullable
                Type underlyingType = Nullable.GetUnderlyingType(typeof(T));
                if (underlyingType == typeof(byte))
                {
                    var v = (byte?)(object)value;
                    return v ?? 0xFF;
                } 
                else 
                {
                    throw new NotSupportedException($"The specified type {typeof(T)} is not supported.");
                }
            } 

            else if ((object)value is null)
                throw new ArgumentNullException(nameof(value));
            else
                throw new NotSupportedException($"The specified type {value.GetType().Name} is not supported.");
        }

    }
}