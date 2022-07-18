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

        protected string LogPrefix => IsLogEnabled ? $"(R) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}" : null;
        public override void Log(string logString, params object[] args)
        {
            if (IsLogEnabled)
                Context.Log.Log(LogPrefix + String.Format(logString, args));
        }

        #endregion

        #region Encoding
        
        public override void DoEncoded(IStreamEncoder encoder, Action action, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            if (encoder == null)
            {
                action();
                return;
            }

            Pointer offset = CurrentPointer;
            long start = Reader.BaseStream.Position;

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Decode the data into a stream
            using var memStream = new MemoryStream();
            encoder.DecodeStream(Reader.BaseStream, memStream);
            memStream.Position = 0;
            long encodedLength = CurrentFileOffset - offset.FileOffset;

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            // Add the stream
            StreamFile sf = new(
                context: Context,
                name: key,
                stream: memStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: offset);

            // Uncomment to test if encoding a stream works using the specified encoder
            /*
            {
                try
                {
                    using var compMemStream = new MemoryStream();
                    encoder.EncodeStream(memStream, compMemStream);
                    memStream.Position = 0;
                    compMemStream.Position = 0;
                    using var decompMemStream = new MemoryStream();
                    encoder.DecodeStream(compMemStream, decompMemStream);

                    if (!decompMemStream.GetBuffer().SequenceEqual(memStream.GetBuffer()))
                        throw new Exception($"Stream encoding failed at {offset}");
                    else
                        Context.Logger.Log("Stream encoding succeeded at {0}", offset);
                }
                catch (NotImplementedException)
                {
                    memStream.Position = 0;
                }
            }
            */

            try
            {
                Context.AddFile(sf);

                DoAt(sf.StartPointer, () =>
                {
                    Log("Decoded data using {0} at {1} with decoded length {2} and encoded length {3}",
                        encoder.Name, offset, sf.Length, encodedLength);

                    action();

                    if (CurrentPointer != sf.StartPointer + sf.Length)
                        LogWarning("Encoded block {0} was not fully deserialized: Serialized size: {1} != Total size: {2}", 
                            key, CurrentPointer - sf.StartPointer, sf.Length);
                });
            }
            finally
            {
                Context.RemoveFile(sf);
            }
        }

        public override Pointer BeginEncoded(IStreamEncoder encoder, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            if (encoder == null) 
                throw new ArgumentNullException(nameof(encoder));
            
            Pointer offset = CurrentPointer;
            long start = Reader.BaseStream.Position;

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Add the stream
            var memStream = new MemoryStream();
            encoder.DecodeStream(Reader.BaseStream, memStream);
            memStream.Position = 0;

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

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
            EncodedState encodedFile = EncodedFiles.FirstOrDefault(ef => ef.File == endPointer.File);

            if (encodedFile == null) 
                return;
            
            EncodedFiles.Remove(encodedFile);

            StreamFile sf = encodedFile.File;
            string key = sf.FilePath;
            
            if (endPointer != sf.StartPointer + sf.Length)
                LogWarning("Encoded block {0} was not fully deserialized: Serialized size: {1} != Total size: {2}", 
                    key, endPointer - sf.StartPointer, sf.Length);

            Context.RemoveFile(sf);
            encodedFile.Stream.Close();
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

            long start = Reader.BaseStream.Position;

            T checksum = (T)ReadAsObject<T>(name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (!checksum.Equals(calculatedChecksum))
                LogWarning("Checksum {0} did not match!", name);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T)}) {name ?? DefaultName}: {checksum} - Checksum to match: {calculatedChecksum} - Matched? {checksum.Equals(calculatedChecksum)}");

            return checksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => Reader.BeginCalculateChecksum(checksumCalculator);

        /// <summary>
        /// Pauses calculating the checksum and returns the current checksum calculator to be used when resuming
        /// </summary>
        /// <returns>The current checksum calculator or null if none is used</returns>
        public override IChecksumCalculator PauseCalculateChecksum() => Reader.PauseCalculateChecksum();

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

            long start = Reader.BaseStream.Position;

            T t = (T)ReadAsObject<T>(name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({typeof(T).Name}) {(name ?? DefaultName)}: {(t?.ToString() ?? "null")}");

            return t;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null)
        {
            bool ignoreCacheOnRead = CurrentFile.IgnoreCacheOnRead || Context.Settings.IgnoreCacheOnRead;

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
                    Context.Log.Log($"{logString}(Object: {typeof(T)}) {name ?? DefaultName}");

                Depth++;
                onPreSerialize?.Invoke(instance);
                instance.Serialize(this);
                Depth--;

                if (isLogTemporarilyDisabled)
                {
                    DisableLogForObject = false;
                    if (IsLogEnabled)
                        Context.Log.Log($"{logString}({typeof(T)}) {name ?? DefaultName}: {instance.ShortLog ?? "null"}");
                }
            }
            else
            {
                Goto(current + instance.Size);
            }
            return instance;
        }

        public override Pointer SerializePointer(Pointer obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            string logString = LogPrefix;

            // Read the pointer value
            long value = size switch
            {
                PointerSize.Pointer16 => Reader.ReadUInt16(),
                PointerSize.Pointer32 => Reader.ReadUInt32(),
                PointerSize.Pointer64 => Reader.ReadInt64(),
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

            BinaryFile currentFile = CurrentFile;

            if (Defaults != null)
            {
                if (anchor == null) 
                    anchor = Defaults.PointerAnchor;

                if (Defaults.PointerFile != null)
                    currentFile = Defaults.PointerFile;
                
                nullValue ??= Defaults.PointerNullValue;
            }

            Pointer ptr = CurrentFile.GetOverridePointer(CurrentAbsoluteOffset);

            if (!nullValue.HasValue || value != nullValue)
            {
                if (ptr != null && anchor != null)
                    ptr = new Pointer(ptr.AbsoluteOffset, ptr.File, anchor, ptr.Size, Pointer.OffsetType.Absolute);

                if (ptr == null)
                {
                    BinaryFile file = currentFile.GetPointerFile(value, anchor);

                    if (file != null)
                        ptr = new Pointer(value, file, anchor, size);
                }

                if (ptr == null && value != 0 && !allowInvalid && !currentFile.AllowInvalidPointer(value, anchor: anchor))
                {
                    if (IsLogEnabled)
                        Context.Log.Log($"{logString}({size}) {name ?? DefaultName}: InvalidPointerException - {value:X16}");

                    throw new PointerException($"Not a valid pointer at {CurrentPointer - 4}: {value:X16}", nameof(SerializePointer));
                }
            }

            if (IsLogEnabled)
                Context.Log.Log($"{logString}({size}) {name ?? DefaultName}: {ptr}");

            return ptr;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer PointerValue = null;
            PointerValue = SerializePointer(PointerValue, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: IsLogEnabled ? $"<{typeof(T)}> {name ?? DefaultName}" : name);
            Pointer<T> p = new Pointer<T>(PointerValue);
            return p;
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null)
        {
            string logString = LogPrefix;

            long origPos = Reader.BaseStream.Position;

            encoding ??= Defaults?.StringEncoding ?? Context.DefaultEncoding;

            var t = length.HasValue ? Reader.ReadString(length.Value, encoding) : Reader.ReadNullDelimitedString(encoding);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(origPos, Reader.BaseStream.Position - origPos);

            if (IsLogEnabled)
                Context.Log.Log($"{logString}(String) {name ?? DefaultName}: {t}");

            return t;
        }

        #endregion

        #region Array Serialization

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null)
        {
            U size = default; // For performance reasons, don't supply this argument
            size = Serialize<U>(size, name: $"{name ?? DefaultName}.Length");

            // Convert size to int, slow
            int intSize = (int)Convert.ChangeType(size, typeof(int));

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
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}: ";
                    byte[] bytes = Reader.ReadBytes((int)count);
                    Context.Log.Log(normalLog + 
                                    bytes.ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
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
                Context.Log.Log($"{logString}({typeof(T).Name}[{count}]) {name ?? DefaultName}");
            }
            T[] buffer;
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
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

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T, int> onPreSerialize = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Object[]: {typeof(T)}[{count}]) {name ?? DefaultName}");
            }
            T[] buffer;
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new T[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeObject<T>(
                    obj: buffer[i], 
                    onPreSerialize: onPreSerialize == null ? (Action<T>)null : x => onPreSerialize(x, i), 
                    name: name == null || !IsLogEnabled ? name : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, string name = null)
        {
            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}({typeof(T).Name}[..]) {name ?? DefaultName}");

            var objects = new List<T>();
            int index = 0;

            while (true)
            {
                var serializedObj = Serialize<T>(default, name: $"{name}[{index}]");

                index++;

                if (conditionCheckFunc(serializedObj))
                {
                    if (getLastObjFunc == null)
                        objects.Add(serializedObj);

                    break;
                }

                objects.Add(serializedObj);
            }

            return objects.ToArray();
        }

        public override T[] SerializeObjectArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null,
            Action<T, int> onPreSerialize = null, string name = null)
        {
            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}(Object[]: {typeof(T)}[..]) {name ?? DefaultName}");

            var objects = new List<T>();
            int index = 0;

            while (true)
            {
                T serializedObj = SerializeObject<T>(
                    obj: default, 
                    onPreSerialize: onPreSerialize == null ? (Action<T>)null : x => onPreSerialize(x, index), 
                    name: $"{name}[{index}]");

                index++;

                if (conditionCheckFunc(serializedObj))
                {
                    if (getLastObjFunc == null)
                        objects.Add(serializedObj);

                    break;
                }

                objects.Add(serializedObj);
            }

            return objects.ToArray();
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}({size}[{count}]) {name ?? DefaultName}");
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
                buffer[i] = SerializePointer(buffer[i], size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(Pointer<{typeof(T)}>[{count}]) {name ?? DefaultName}");
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
                buffer[i] = SerializePointer<T>(
                    obj: buffer[i], 
                    size: size, 
                    anchor: anchor, 
                    allowInvalid: allowInvalid, 
                    nullValue: nullValue, 
                    name: name == null || !IsLogEnabled ? name : $"{name}[{i}]");

            return buffer;
        }

        public override string[] SerializeStringArray(string[] obj, long count, long? length = null, Encoding encoding = null, string name = null)
        {
            if (IsLogEnabled)
            {
                string logString = LogPrefix;
                Context.Log.Log($"{logString}(String[{count}]) {name ?? DefaultName}");
            }
            string[] buffer;
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
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
                    logs.Add($"{logPrefix}  (UInt{length}) {name ?? DefaultName}: {bitValue}");

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
            Type type = typeof(T);

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode)
            {
                case TypeCode.Boolean:
                    var b = Reader.ReadByte();

                    if (b != 0 && b != 1)
                    {
                        LogWarning("Binary boolean '{0}' ({1}) was not correctly formatted", name, b);

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
                    return Reader.ReadNullDelimitedString(Defaults?.StringEncoding ?? Context.DefaultEncoding);

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

            if (CurrentFile == file)
                CurrentFile = null;
        }

        #endregion
    }
}