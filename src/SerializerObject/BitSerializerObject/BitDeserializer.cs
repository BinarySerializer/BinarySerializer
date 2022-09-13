#nullable enable
using System;

namespace BinarySerializer 
{
    public class BitDeserializer : BitSerializerObject 
    {
        public BitDeserializer(SerializerObject serializerObject, Pointer valueOffset, string? logPrefix, long value) 
            : base(serializerObject, valueOffset, logPrefix, value) { }

        public override T SerializeBits<T>(
            T value, 
            int length, 
            SignedNumberRepresentation sign = SignedNumberRepresentation.Unsigned, 
            string? name = null) 
        {
            long bitValue = BitHelpers.ExtractBits64(Value, length, Position, sign: sign);
            T t = (T)LongToObject<T>(bitValue, name: name);

            if (SerializerObject.IsSerializerLogEnabled && !DisableSerializerLogForObject)
                Context.SerializerLog.Log($"{LogPrefix}{Position}_{length} ({typeof(T).Name}) {name ?? DefaultName}: {(t?.ToString() ?? "null")}");

            Position += length;

            return t;
        }

        public override T SerializeObject<T>(T? obj, Action<T>? onPreSerialize = null, string? name = null) 
            where T : class
        {
            // There is no caching for BitSerializable objects
            T instance = new();

            long pos = Position;

            // Initialize the instance
            instance.Init(ValueOffset, pos);

            string? logString = LogPrefix;
            bool isLogTemporarilyDisabled = false;
            
            if (!DisableSerializerLogForObject && instance.UseShortLog) 
            {
                DisableSerializerLogForObject = true;
                isLogTemporarilyDisabled = true;
            }

            if (SerializerObject.IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{logString}{pos} (Object: {typeof(T)}) {name ?? DefaultName}");

            try 
            {
                Depth++;
                onPreSerialize?.Invoke(instance);
                instance.Serialize(this);
            } 
            finally 
            {
                Depth--;

                if (isLogTemporarilyDisabled) 
                {
                    DisableSerializerLogForObject = false;

                    if (SerializerObject.IsSerializerLogEnabled)
                        Context.SerializerLog.Log($"{logString}{pos}_{instance.Size} ({typeof(T)}) {name ?? DefaultName}: {instance.ShortLog ?? "null"}");
                }
            }
            return instance;
        }

        protected object LongToObject<T>(long input, string? name = null) 
        {
            // Get the type
            Type type = typeof(T);

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode) 
            {
                case TypeCode.Boolean:
                    if (input != 0 && input != 1) 
                    {
                        Context.SystemLog?.LogWarning("Binary boolean '{0}' ({1}) was not correctly formatted", name, input);

                        if (SerializerObject.IsSerializerLogEnabled)
                            Context.SerializerLog.Log($"{LogPrefix} ({typeof(T)}): Binary boolean was not correctly formatted ({input})");
                    }

                    return input != 0;

                case TypeCode.SByte:
                    return (sbyte)input;

                case TypeCode.Byte:
                    return (byte)input;

                case TypeCode.Int16:
                    return (short)input;

                case TypeCode.UInt16:
                    return (ushort)input;

                case TypeCode.Int32:
                    return (int)input;

                case TypeCode.UInt32:
                    return (uint)input;

                case TypeCode.Int64:
                    return (long)input;

                case TypeCode.UInt64:
                    return BitConverter.ToUInt64(BitConverter.GetBytes(input), 0);

                case TypeCode.Single:
                    return BitConverter.ToSingle(BitConverter.GetBytes((int)input), 0);

                case TypeCode.Double:
                    return BitConverter.ToDouble(BitConverter.GetBytes(input), 0);

                case TypeCode.Decimal:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.Object:
                    if (type == typeof(UInt24)) 
                    {
                        return new UInt24((uint)input);
                    } 
                    else if (type == typeof(byte?)) 
                    {
                        byte nullableByte = (byte)input;
                        if (nullableByte == 0xFF) 
                            return (byte?)null!;
                        return nullableByte;
                    } 
                    else 
                    {
                        throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the BitDeserializer");
                    }
                default:
                    throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the BitDeserializer");
            }
        }
    }
}