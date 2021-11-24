using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BinarySerializer
{
    /// <summary>
    /// A binary serializer used for deserializing
    /// </summary>
    public class BinaryDeserializer : SerializerObject, IDisposable 
    {
        #region Constructor

        public BinaryDeserializer(Context context) : base(context)
        {
            Readers = new Dictionary<BinaryFile, Reader>();
        }

        #endregion

        #region Protected Properties

        protected Dictionary<BinaryFile, Reader> Readers { get; }
        protected Reader Reader { get; set; }
        protected BinaryFile CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => Reader.BaseStream.Length;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile;

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => Reader.BaseStream.Position;

        #endregion

        #region Logging

        protected string LogPrefix => IsLogEnabled ? ($"(R) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}") : null;
        public override void Log(string logString)
        {
            if (IsLogEnabled)
                Context.Log.Log(LogPrefix + logString);
        }

        #endregion

        #region Encoding

        public override void DoEncoded(IStreamEncoder encoder, Action action, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            Pointer offset = CurrentPointer;

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Decode the data into a stream
            using var memStream = encoder.DecodeStream(Reader.BaseStream);

            // Add the stream
            StreamFile sf = new StreamFile(
                context: Context,
                name: key,
                stream: memStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: offset);

            try
            {
                Context.AddFile(sf);

                DoAt(sf.StartPointer, () =>
                {
                    action();

                    if (CurrentPointer != sf.StartPointer + sf.Length)
                        LogWarning($"Encoded block {key} was not fully deserialized: Serialized size: {CurrentPointer - sf.StartPointer} != Total size: {sf.Length}");
                });
            }
            finally
            {
                Context.RemoveFile(sf);
            }
        }

        public override Pointer BeginEncoded(IStreamEncoder encoder, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            Pointer offset = CurrentPointer;

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Add the stream
            Stream memStream = encoder.DecodeStream(Reader.BaseStream);
            StreamFile sf = new StreamFile(
                context: Context, 
                name: key, 
                stream: memStream, 
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: offset);
            Context.AddFile(sf);
            EncodedFiles.Add(new EncodedState()
            {
                File = sf,
                Stream = memStream,
                Encoder = encoder
            });

            return sf.StartPointer;
        }

        public override void EndEncoded(Pointer endPointer)
        {
            var encodedFile = EncodedFiles.FirstOrDefault(ef => ef.File == endPointer.File);
            if (encodedFile != null)
            {
                EncodedFiles.Remove(encodedFile);

                var sf = encodedFile.File;
                var key = sf.FilePath;
                if (endPointer != sf.StartPointer + sf.Length)
                {
                    LogWarning($"Encoded block {key} was not fully deserialized: Serialized size: {endPointer - sf.StartPointer} != Total size: {sf.Length}");
                }

                Context.RemoveFile(sf);
                encodedFile.Stream.Close();
            }
        }

        #endregion

        #region XOR

        public override void BeginXOR(IXORCalculator xorCalculator) => Reader.BeginXOR(xorCalculator);
        public override void EndXOR() => Reader.EndXOR();
        public override IXORCalculator GetXOR() => Reader.GetXORCalculator();

        #endregion

        #region Positioning

        public override void Goto(Pointer offset)
        {
            if (offset == null)
                return;

            if (offset.File != CurrentFile)
                SwitchToFile(offset.File);

            Reader.BaseStream.Position = offset.FileOffset;
        }

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string name = null)
        {
            string logString = LogPrefix;

            var start = Reader.BaseStream.Position;

            T checksum = (T)ReadAsObject<T>(name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (!checksum.Equals(calculatedChecksum))
                LogWarning($"Checksum {name} did not match!");

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {checksum} - Checksum to match: {calculatedChecksum} - Matched? {checksum.Equals(calculatedChecksum)}");

            return checksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => Reader.BeginCalculateChecksum(checksumCalculator);

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <returns>The checksum value</returns>
        public override T EndCalculateChecksum<T>() => Reader.EndCalculateChecksum<T>();

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string name = null)
        {
            string logString = LogPrefix;

            var start = Reader.BaseStream.Position;

            T t = (T)ReadAsObject<T>(name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T).Name}) {(name ?? "<no name>")}: {(t?.ToString() ?? "null")}");

            return t;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null)
        {
            var ignoreCacheOnRead = CurrentFile.IgnoreCacheOnRead || Context.Settings.IgnoreCacheOnRead;

            // Get the current pointer
            Pointer current = CurrentPointer;

            // Attempt to get a cached instance of the object if caching is enabled
            T instance = ignoreCacheOnRead ? null : Context.Cache.FromOffset<T>(current);

            // If we did not get a cached object we create a new one
            if (instance == null)
            {
                // Create a new instance
                instance = new T();

                // Initialize the instance
                instance.Init(current);

                // Cache the object if caching is enabled
                if (!ignoreCacheOnRead)
                    Context.Cache.Add<T>(instance);

                string logString = IsLogEnabled ? LogPrefix : null;
                bool isLogTemporarilyDisabled = false;
                if (!DisableLogForObject && instance.UseShortLog)
                {
                    DisableLogForObject = true;
                    isLogTemporarilyDisabled = true;
                }

                if (IsLogEnabled) 
                    Context.Log.Log($"{logString}(Object: {typeof(T)}) {(name ?? "<no name>")}");

                Depth++;
                onPreSerialize?.Invoke(instance);
                instance.Serialize(this);
                Depth--;

                if (isLogTemporarilyDisabled)
                {
                    DisableLogForObject = false;
                    if (IsLogEnabled)
                        Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {(instance.ShortLog ?? "null")}");
                }
            }
            else
            {
                Goto(current + instance.Size);
            }
            return instance;
        }

        public override Pointer SerializePointer(Pointer obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, string name = null)
        {
            string logString = LogPrefix;

            // Read the pointer value
            var value = size switch
            {
                PointerSize.Pointer16 => Reader.ReadUInt16(),
                PointerSize.Pointer32 => Reader.ReadUInt32(),
                PointerSize.Pointer64 => Reader.ReadInt64(),
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

            Pointer ptr = CurrentFile.GetOverridePointer(CurrentAbsoluteOffset);

            if (ptr != null && anchor != null)
                ptr = new Pointer(ptr.AbsoluteOffset, ptr.File, anchor, ptr.Size, Pointer.OffsetType.Absolute);

            if (ptr == null)
            {
                var file = CurrentFile.GetPointerFile(value, anchor);

                if (file != null)
                    ptr = new Pointer(value, file, anchor, size);
            }

            if (ptr == null && value != 0 && !allowInvalid && !CurrentFile.AllowInvalidPointer(value, anchor: anchor))
            {
                if (IsLogEnabled)
                    Context.Log.Log($"{logString}({size}) {name ?? "<no name>"}: InvalidPointerException - {value:X16}");

                throw new PointerException($"Not a valid pointer at {CurrentPointer - 4}: {value:X16}", nameof(SerializePointer));
            }

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({size}) {name ?? "<no name>"}: {ptr}");

            return ptr;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer<T>: {typeof(T)}) {(name ?? "<no name>")}");
            }

            Depth++;
            Pointer<T> p = new Pointer<T>(this, size: size, anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid);
            Depth--;
            return p;
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null)
        {
            string logString = LogPrefix;

            long origPos = Reader.BaseStream.Position;

            var t = length.HasValue ? Reader.ReadString(length.Value, encoding ?? Context.DefaultEncoding) : Reader.ReadNullDelimitedString(encoding ?? Context.DefaultEncoding);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(origPos, Reader.BaseStream.Position - origPos);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}(String) {(name ?? "<no name>")}: {t}");

            return t;
        }

        #endregion

        #region Array Serialization

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null)
        {
            //U Size = (U)Convert.ChangeType((obj?.Length) ?? 0, typeof(U));
            U Size = default; // For performance reasons, don't supply this argument
            Size = Serialize<U>(Size, name: $"{name}.Length");
            // Convert size to int, slow
            int intSize = (int)Convert.ChangeType(Size, typeof(int));

            if (obj == null)
                obj = new T[intSize];
            else if (obj.Length != intSize)
                Array.Resize(ref obj, intSize);
            return obj;
        }

        public override T[] SerializeArray<T>(T[] obj, long count, string name = null)
        {
            // Use byte reading method if requested
            if (typeof(T) == typeof(byte))
            {
                if (CurrentFile.ShouldUpdateReadMap)
                    CurrentFile.UpdateReadMap(Reader.BaseStream.Position, count);
                if (IsLogEnabled)
                {
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {(name ?? "<no name>")}: ";
                    byte[] bytes = Reader.ReadBytes((int)count);
                    Context.Log.Log(normalLog
                        + bytes.ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
                    return (T[])(object)bytes;
                }
                else
                {
                    return (T[])(object)Reader.ReadBytes((int)count);
                }
            }
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}({typeof(T).Name}[{count}]) {(name ?? "<no name>")}");
            }
            T[] buffer;
            if (obj != null)
            {
                buffer = obj;
                if (buffer.Length != count)
                {
                    Array.Resize(ref buffer, (int)count);
                }
            }
            else
            {
                buffer = new T[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = Serialize<T>(buffer[i], name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Object[]: {typeof(T)}[{count}]) {(name ?? "<no name>")}");
            }
            T[] buffer;
            if (obj != null)
            {
                buffer = obj;
                if (buffer.Length != count)
                {
                    Array.Resize(ref buffer, (int)count);
                }
            }
            else
            {
                buffer = new T[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeObject<T>(buffer[i], onPreSerialize: onPreSerialize, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}({size}[{count}]) {(name ?? "<no name>")}");
            }

            Pointer[] buffer;

            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new Pointer[count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer(buffer[i], size: size, anchor: anchor, allowInvalid: allowInvalid, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer<{typeof(T)}>[{count}]) {(name ?? "<no name>")}");
            }

            Pointer<T>[] buffer;

            if (obj != null)
            {
                buffer = obj;
                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new Pointer<T>[count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer<T>(buffer[i], size: size, anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override string[] SerializeStringArray(string[] obj, long count, int length, Encoding encoding = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(String[{count}]) {name ?? "<no name>"}");
            }
            string[] buffer;
            if (obj != null)
            {
                buffer = obj;
                if (buffer.Length != count)
                {
                    Array.Resize(ref buffer, (int)count);
                }
            }
            else
            {
                buffer = new string[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeString(default, length, encoding, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action)
        {
            Reader r = Reader;
            bool isLittleEndian = r.IsLittleEndian;
            if (isLittleEndian != (endianness == Endian.Little))
            {
                r.IsLittleEndian = (endianness == Endian.Little);
                action();
                r.IsLittleEndian = isLittleEndian;
            }
            else
            {
                action();
            }
        }

        public override void SerializeBitValues(Action<SerializeBits64> serializeFunc)
        {
            string logPrefix = LogPrefix;

            // Extract bits
            int pos = 0;
            
            using var buffer = new MemoryStream();
            var logs = IsLogEnabled ? new List<string>() : null;

            serializeFunc((v, length, name) => 
            {
                int bitsFromPrevByte = 0;

                if (pos % 8 != 0)
                    bitsFromPrevByte = 8 - pos % 8;

                int bitsToRead = length - bitsFromPrevByte;
                int bytesToRead = (int)Math.Ceiling(bitsToRead / 8f);

                for (int i = 0; i < bytesToRead; i++)
                    buffer.WriteByte(Serialize<byte>(default, name: IsLogEnabled ? $"Value[{buffer.Length}]" : null));

                long bitValue = BitHelpers.ExtractBits64(buffer.GetBuffer(), length, pos);

                if (IsLogEnabled)
                    logs.Add($"{logPrefix}  (UInt{length}) {name ?? "<no name>"}: {bitValue}");

                pos += length;
                return bitValue;
            });

            if (IsLogEnabled)
                foreach (string l in logs)
                    Context.Log.Log(l);
        }

        public override void DoBits<T>(Action<BitSerializerObject> serializeFunc) 
        {
            string logPrefix = LogPrefix;
            Pointer p = CurrentPointer;
            long value = Convert.ToInt64(Serialize<T>(default, name: "Value"));
            serializeFunc(new BitDeserializer(this, p, logPrefix, value));
        }

        #endregion

        #region Caching

        public override Task FillCacheForReadAsync(long length) => FileManager.FillCacheForReadAsync(length, Reader);

        #endregion

        #region Protected Helpers

        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null)
                return;

            if (!Readers.ContainsKey(newFile))
            {
                Readers.Add(newFile, newFile.CreateReader());
                newFile.InitFileReadMap(Readers[newFile].BaseStream.Length);
            }

            Reader = Readers[newFile];
            CurrentFile = newFile;
        }

        // Helper method which returns an object so we can cast it
        protected object ReadAsObject<T>(string name = null)
        {
            // Get the type
            var type = typeof(T);

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode)
            {
                case TypeCode.Boolean:
                    var b = Reader.ReadByte();

                    if (b != 0 && b != 1)
                    {
                        LogWarning($"Binary boolean '{name}' ({b}) was not correctly formatted");

                        if (IsLogEnabled)
                            Context.Log.Log($"{LogPrefix} ({typeof(T)}): Binary boolean was not correctly formatted ({b})");
                    }

                    return b != 0;

                case TypeCode.SByte:
                    return Reader.ReadSByte();

                case TypeCode.Byte:
                    return Reader.ReadByte();

                case TypeCode.Int16:
                    return Reader.ReadInt16();

                case TypeCode.UInt16:
                    return Reader.ReadUInt16();

                case TypeCode.Int32:
                    return Reader.ReadInt32();

                case TypeCode.UInt32:
                    return Reader.ReadUInt32();

                case TypeCode.Int64:
                    return Reader.ReadInt64();

                case TypeCode.UInt64:
                    return Reader.ReadUInt64();

                case TypeCode.Single:
                    return Reader.ReadSingle();

                case TypeCode.Double:
                    return Reader.ReadDouble();
                case TypeCode.String:
                    return Reader.ReadNullDelimitedString(Context.DefaultEncoding);

                case TypeCode.Decimal:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Empty:
                case TypeCode.DBNull:
                case TypeCode.Object:
                    if (type == typeof(UInt24))
                    {
                        return Reader.ReadUInt24();
                    }
                    else if (type == typeof(byte?))
                    {
                        byte nullableByte = Reader.ReadByte();
                        if (nullableByte == 0xFF) return (byte?)null;
                        return nullableByte;
                    }
                    else
                    {
                        throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the reader");
                    }
                default:
                    throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the reader");
            }
        }

        #endregion

        #region Disposing

        public void Dispose()
        {
            foreach (KeyValuePair<BinaryFile, Reader> r in Readers)
                r.Key.EndRead(r.Value);

            Readers.Clear();
            Reader = null;
        }

        public void DisposeFile(BinaryFile file)
        {
            if (!Readers.ContainsKey(file))
                return;

            Reader r = Readers[file];
            file.EndRead(r);
            Readers.Remove(file);
        }

        #endregion
    }
}