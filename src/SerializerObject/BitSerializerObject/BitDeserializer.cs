using System;

namespace BinarySerializer {
    public class BitDeserializer : BitSerializerObject {
        public BitDeserializer(SerializerObject serializerObject, string logPrefix, long value) : base(serializerObject, logPrefix, value) { }

        public override T SerializeBit<T>(T value, int length, string name = null) {
            var bitValue = BitHelpers.ExtractBits64(Value, length, Position);
            T t = (T)LongToObject<T>(bitValue, name: name);

            if (SerializerObject.IsLogEnabled)
                Context.Log.Log($"{LogPrefix}  (UInt{length} -> {typeof(T).Name}) {(name ?? "<no name>")}: {(t?.ToString() ?? "null")}");

            Position += length;

            return t;
        }

        protected object LongToObject<T>(long input, string name = null) {
            // Get the type
            var type = typeof(T);

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode) {
                case TypeCode.Boolean:
                    var b = input;

                    if (b != 0 && b != 1) {
                        SerializerObject.LogWarning($"Binary boolean '{name}' ({b}) was not correctly formatted");

                        if (SerializerObject.IsLogEnabled)
                            Context.Log.Log($"{LogPrefix} ({typeof(T)}): Binary boolean was not correctly formatted ({b})");
                    }

                    return b != 0;

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
                    if (type == typeof(UInt24)) {
                        return new UInt24((uint)input);
                    } else if (type == typeof(byte?)) {
                        byte nullableByte = (byte)input;
                        if (nullableByte == 0xFF) return (byte?)null;
                        return nullableByte;
                    } else {
                        throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the BitDeserializer");
                    }
                default:
                    throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the BitDeserializer");
            }
        }

    }
}
