#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
        protected Reader? Reader { get; set; }
        protected BinaryFile? CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => Reader?.BaseStream.Length ?? throw new SerializerMissingCurrentPointerException();

        [MemberNotNullWhen(true, nameof(CurrentFile), nameof(Reader))]
        public override bool HasCurrentPointer => CurrentFile != null && Reader != null;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile ?? throw new SerializerMissingCurrentPointerException();

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => Reader?.BaseStream.Position ?? throw new SerializerMissingCurrentPointerException();

        #endregion

        #region Logging

        protected string? LogPrefix => IsSerializerLogEnabled ? $"(R) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}" : null;

        protected void WriteLogPrefix() => WriteLogPrefix(CurrentPointer, Depth);
        protected void WriteLogPrefix(Pointer pointer) => WriteLogPrefix(pointer, Depth);
        protected void WriteLogPrefix(Pointer pointer, int depth)
        {
            SerializerLog.Write("(R) ");
            SerializerLog.Write(pointer);
            SerializerLog.Write(":");
            SerializerLog.Write(new string(' ', (depth + 1) * 2));
        }
        protected void WriteLogDataType(string typeName)
        {
            SerializerLog.Write("(");
            SerializerLog.Write(typeName);
            SerializerLog.Write(") ");
        }
        protected void WriteLogDataType(string typeName, long? arrayLength)
        {
            SerializerLog.Write("(");
            SerializerLog.Write(typeName);
            SerializerLog.Write("[");
            SerializerLog.Write(arrayLength?.ToString() ?? "..");
            SerializerLog.Write("]) ");
        }
        protected void WriteLogDataName(string? name, bool hasValue = true)
        {
            SerializerLog.Write(name ?? DefaultName);

            if (hasValue)
                SerializerLog.Write(": ");
        }
        protected void WriteLogData(object? value)
        {
            SerializerLog.Write(value ?? "null");
        }
        protected void WriteLogLineEnd() => SerializerLog.WriteLine(null);
        public override void Log(string logString, params object?[] args)
        {
            if (!IsSerializerLogEnabled)
                return;
            
            WriteLogPrefix();
            SerializerLog.WriteLine(String.Format(logString, args));
        }

        #endregion

        #region Encoding
        
        public override void DoEncoded(
            IStreamEncoder? encoder, 
            Action action, 
            Endian? endianness = null, 
            bool allowLocalPointers = false, 
            string? filename = null)
        {
            if (action == null) 
                throw new ArgumentNullException(nameof(action));
            
            if (encoder == null)
            {
                action();
                return;
            }

            VerifyHasCurrentPointer();

            Pointer offset = CurrentPointer;
            long start = Reader.BaseStream.Position;

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Decode the data into a stream
            using MemoryStream memStream = new();
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
                        SystemLog?.LogWarning("Encoded block {0} was not fully deserialized: Serialized size: {1} != Total size: {2}", 
                            key, CurrentPointer - sf.StartPointer, sf.Length);
                });
            }
            finally
            {
                Context.RemoveFile(sf);
            }
        }

        public override Pointer BeginEncoded(
            IStreamEncoder encoder,
            Endian? endianness = null, 
            bool allowLocalPointers = false, 
            string? filename = null)
        {
            if (encoder == null) 
                throw new ArgumentNullException(nameof(encoder));

            VerifyHasCurrentPointer();
            
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

            StreamFile sf = new(
                context: Context, 
                name: key, 
                stream: memStream, 
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: offset);
            Context.AddFile(sf);
            EncodedFiles.Add(new EncodedState(memStream, sf, encoder));

            return sf.StartPointer;
        }

        public override void EndEncoded(Pointer endPointer)
        {
            if (endPointer == null) 
                throw new ArgumentNullException(nameof(endPointer));
            
            EncodedState? encodedFile = EncodedFiles.FirstOrDefault(ef => ef.File == endPointer.File);

            if (encodedFile == null) 
                return;
            
            EncodedFiles.Remove(encodedFile);

            StreamFile sf = encodedFile.File;
            string key = sf.FilePath;
            
            if (endPointer != sf.StartPointer + sf.Length)
                SystemLog?.LogWarning("Encoded block {0} was not fully deserialized: Serialized size: {1} != Total size: {2}", 
                    key, endPointer - sf.StartPointer, sf.Length);

            Context.RemoveFile(sf);
            encodedFile.Stream.Close();
        }

        #endregion

        #region XOR

        public override void BeginXOR(IXORCalculator? xorCalculator)
        {
            VerifyHasCurrentPointer();
            Reader.BeginXOR(xorCalculator);
        }

        public override void EndXOR()
        {
            VerifyHasCurrentPointer();
            Reader.EndXOR();
        }

        public override IXORCalculator? GetXOR()
        {
            VerifyHasCurrentPointer();
            return Reader.GetXORCalculator();
        }

        #endregion

        #region Positioning

        public override void Goto(Pointer? offset)
        {
            if (offset == null)
                return;

            if (offset.File != CurrentFile || !HasCurrentPointer)
                SwitchToFile(offset.File);

            Reader.BaseStream.Position = offset.FileOffset;
        }

        public override void Align(int alignBytes = 4, Pointer? baseOffset = null, bool? logIfNotNull = null)
        {
            VerifyHasCurrentPointer();

            long align = (CurrentAbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes;

            // Make sure we need to align
            if (align == 0) 
                return;
            
            long count = alignBytes - align;

            Pointer? startPointer = IsSerializerLogEnabled ? CurrentPointer : null;

            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                SerializerLog.Write("Align ");
                SerializerLog.WriteLine(alignBytes);
            }

            logIfNotNull ??= Context.Settings.LogAlignIfNotNull;

            // If we log we have to read the bytes to check that they are not null
            if (logIfNotNull.Value)
            {
                byte[] bytes = Reader.ReadBytes((int)count);

                // Check if any bytes are not null
                if (bytes.Any(x => x != 0))
                {
                    if (IsSerializerLogEnabled)
                    {
                        WriteLogPrefix(startPointer!);
                        WriteLogDataType(nameof(Byte), count);
                        SerializerLog.Write("Align: ");
                        // TODO: Return to this
                        //log += bytes.ToHexString(align: 16, newLinePrefix: new string(' ', log.Length), maxLines: 10);
                        //Context.SerializerLog.Log(log);
                    }

                    SystemLog?.LogWarning("Align bytes at {0} contains data! Data: {1}", 
                        CurrentPointer - count, bytes.ToHexString(align: 16, maxLines: 1));
                }
            }
            else
            {
                Goto(CurrentPointer + count);
            }
        }

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
                WriteLogPrefix();

            long start = Reader.BaseStream.Position;

            T checksum = (T)ReadAsObject(typeof(T), name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            bool match = checksum.Equals(calculatedChecksum);

            if (!match)
                SystemLog?.LogWarning("Checksum {0} did not match!", name);

            if (IsSerializerLogEnabled)
            {
                WriteLogDataType(typeof(T).ToString());
                WriteLogDataName(name);
                SerializerLog.Write(checksum);
                SerializerLog.Write(" - Checksum to match: ");
                SerializerLog.Write(calculatedChecksum);
                SerializerLog.Write(" - Matched? ");
                SerializerLog.WriteLine(match);
            }

            return checksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator? checksumCalculator)
        {
            VerifyHasCurrentPointer();
            Reader.BeginCalculateChecksum(checksumCalculator);
        }

        /// <summary>
        /// Pauses calculating the checksum and returns the current checksum calculator to be used when resuming
        /// </summary>
        /// <returns>The current checksum calculator or null if none is used</returns>
        public override IChecksumCalculator? PauseCalculateChecksum()
        {
            VerifyHasCurrentPointer();
            return Reader.PauseCalculateChecksum();
        }

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <param name="value">The default value to return if no value exists</param>
        /// <returns>The checksum value</returns>
        public override T EndCalculateChecksum<T>(T value)
        {
            VerifyHasCurrentPointer();
            return Reader.EndCalculateChecksum<T>();
        }

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
                WriteLogPrefix();

            long start = Reader.BaseStream.Position;

            Type type = typeof(T);

            T t = (T)ReadAsObject(type, name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (IsSerializerLogEnabled)
            {
                WriteLogDataType(type.Name);
                WriteLogDataName(name);
                WriteLogData(t);
                WriteLogLineEnd();
            }

            return t;
        }

        public override T? SerializeNullable<T>(T? obj, string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
                WriteLogPrefix();

            long start = Reader.BaseStream.Position;

            Type type = typeof(T);

            T? t = (T?)ReadAsNullableObject(type, name);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(start, Reader.BaseStream.Position - start);

            if (IsSerializerLogEnabled)
            {
                WriteLogDataType($"{type.Name}?");
                WriteLogDataName(name);
                WriteLogData(t);
                WriteLogLineEnd();
            }

            return t;
        }

        public override T SerializeObject<T>(T? obj, Action<T>? onPreSerialize = null, string? name = null)
            where T : class
        {
            VerifyHasCurrentPointer();

            bool ignoreCacheOnRead = CurrentFile.IgnoreCacheOnRead || Context.Settings.IgnoreCacheOnRead;

            // Get the current pointer
            Pointer current = CurrentPointer;

            // Attempt to get a cached instance of the object if caching is enabled
            T? instance = ignoreCacheOnRead ? null : Context.Cache.FromOffset<T>(current);

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

                bool isLogTemporarilyDisabled = false;
                if (!DisableSerializerLogForObject && instance.UseShortLog)
                {
                    DisableSerializerLogForObject = true;
                    isLogTemporarilyDisabled = true;
                }

                if (IsSerializerLogEnabled)
                {
                    WriteLogPrefix(current);
                    WriteLogDataType($"Object: {typeof(T)}");
                    WriteLogDataName(name, hasValue: false);
                    WriteLogLineEnd();
                }

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
                        if (IsSerializerLogEnabled)
                        {
                            WriteLogPrefix(current);
                            WriteLogDataType(typeof(T).ToString());
                            WriteLogDataName(name);
                            WriteLogData(instance.ShortLog);
                            WriteLogLineEnd();
                        }
                    }
                }
            }
            else
            {
                Goto(current + instance.Size);
            }
            return instance;
        }

        public override Pointer? SerializePointer(
            Pointer? obj, 
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null, 
            string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
                WriteLogPrefix();

            BinaryFile currentFile = CurrentFile;

            if (Defaults != null) 
            {
                if (anchor == null)
                    anchor = Defaults.PointerAnchor;

                if (Defaults.PointerFile != null)
                    currentFile = Defaults.PointerFile;

                nullValue ??= Defaults.PointerNullValue;
            }

            Pointer? ptr = CurrentFile.GetOverridePointer(CurrentAbsoluteOffset);

            // Read the pointer value
            long value = size switch
            {
                PointerSize.Pointer16 => Reader.ReadUInt16(),
                PointerSize.Pointer32 => Reader.ReadUInt32(),
                PointerSize.Pointer64 => Reader.ReadInt64(),
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

            if (!nullValue.HasValue || value != nullValue)
            {
                if (ptr != null && anchor != null)
                    ptr = new Pointer(ptr.AbsoluteOffset, ptr.File, anchor, ptr.Size, Pointer.OffsetType.Absolute);

                if (ptr == null)
                {
                    if (!currentFile.TryGetPointer(value, out ptr, anchor: anchor, allowInvalid: allowInvalid, size: size)) 
                    {
                        // Invalid pointer
                        if (IsSerializerLogEnabled)
                        {
                            WriteLogDataType(size.ToString());
                            WriteLogDataName(name);
                            SerializerLog.WriteLine($"InvalidPointerException - {value:X16}");
                        }

                        throw new PointerException($"Not a valid pointer at {CurrentPointer - (int)size}: {value:X16}", nameof(SerializePointer));
                    }
                }
            }

            if (IsSerializerLogEnabled)
            {
                WriteLogDataType(size.ToString());
                WriteLogDataName(name);
                SerializerLog.WriteLine(ptr);
            }

            return ptr;
        }

        public override Pointer<T> SerializePointer<T>(
            Pointer<T>? obj, 
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null, 
            string? name = null)
        {
            Pointer? pointerValue = SerializePointer(
                obj: null, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: IsSerializerLogEnabled ? $"<{typeof(T)}> {name ?? DefaultName}" : name);
            return new Pointer<T>(pointerValue);
        }

        public override string SerializeString(string? obj, long? length = null, Encoding? encoding = null, string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
                WriteLogPrefix();

            long origPos = Reader.BaseStream.Position;

            encoding ??= Defaults?.StringEncoding ?? Context.DefaultEncoding;

            string t = length.HasValue ? Reader.ReadString(length.Value, encoding) : Reader.ReadNullDelimitedString(encoding);

            if (CurrentFile.ShouldUpdateReadMap)
                CurrentFile.UpdateReadMap(origPos, Reader.BaseStream.Position - origPos);

            if (IsSerializerLogEnabled)
            {
                WriteLogDataType(nameof(String));
                WriteLogDataName(name);
                WriteLogData(t);
                WriteLogLineEnd();
            }

            return t;
        }

        #endregion

        #region Array Serialization

        public override T?[] SerializeArraySize<T, U>(T?[]? obj, string? name = null)
            where T : default
        {
            U size = Serialize<U>(default, name: $"{name ?? DefaultName}.Length");

            // Convert size to int, slow
            int intSize = (int)Convert.ChangeType(size, typeof(int));

            if (obj == null)
                obj = new T[intSize];
            else if (obj.Length != intSize)
                Array.Resize(ref obj, intSize);

            return obj;
        }

        public override T[] SerializeArray<T>(T[]? obj, long count, string? name = null)
        {
            VerifyHasCurrentPointer();

            // Use byte reading method if requested
            if (typeof(T) == typeof(byte))
            {
                if (CurrentFile.ShouldUpdateReadMap)
                    CurrentFile.UpdateReadMap(Reader.BaseStream.Position, count);

                if (IsSerializerLogEnabled)
                {
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}: ";
                    byte[] bytes = Reader.ReadBytes((int)count);
                    Context.SerializerLog.Log(normalLog + 
                                    bytes.ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
                    return (T[])(object)bytes;
                }
                else
                {
                    return (T[])(object)Reader.ReadBytes((int)count);
                }
            }
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType(typeof(T).Name, count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
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
                buffer[i] = Serialize<T>(buffer[i], name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer;
        }

        public override T?[] SerializeNullableArray<T>(T?[]? obj, long count, string? name = null)
        {
            VerifyHasCurrentPointer();

            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType($"{typeof(T).Name}?", count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            T?[] buffer;
            
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new T?[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeNullable<T>(buffer[i], name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T?[]? obj, long count, Action<T, int>? onPreSerialize = null, string? name = null)
            where T : class
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType($"Object[]: {typeof(T)}", count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }
            T?[] buffer;
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new T?[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeObject<T>(
                    obj: buffer[i], 
                    // ReSharper disable once AccessToModifiedClosure
                    onPreSerialize: onPreSerialize == null ? (Action<T>?)null : x => onPreSerialize(x, i), 
                    name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer!;
        }

        public override Pointer?[] SerializePointerArray(
            Pointer?[]? obj,
            long count,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null,
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType(size.ToString(), count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            Pointer?[] buffer;

            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new Pointer?[count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer(buffer[i], size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(
            Pointer<T>?[]? obj,
            long count,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null,
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType($"{size}<{typeof(T)}>", count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            Pointer<T>?[] buffer;

            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new Pointer<T>?[count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializePointer<T>(
                    obj: buffer[i],
                    size: size,
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue,
                    name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer!;
        }

        public override string[] SerializeStringArray(
            string?[]? obj,
            long count,
            long? length = null,
            Encoding? encoding = null,
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType(nameof(String), count);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }
            string?[] buffer;
            if (obj != null)
            {
                buffer = obj;

                if (buffer.Length != count)
                    Array.Resize(ref buffer, (int)count);
            }
            else
            {
                buffer = new string?[(int)count];
            }

            for (int i = 0; i < count; i++)
                // Read the value
                buffer[i] = SerializeString(buffer[i], length, encoding, name: IsSerializerLogEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer!;
        }

        public override T[] SerializeArrayUntil<T>(
            T[]? obj,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType(typeof(T).Name, null);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            List<T> objects = new();
            int index = 0;

            while (true)
            {
                T serializedObj = Serialize<T>(default, name: $"{name ?? DefaultName}[{index}]");

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

        public override T?[] SerializeNullableArrayUntil<T>(
            T?[]? obj, 
            Func<T?, bool> conditionCheckFunc, 
            Func<T?>? getLastObjFunc = null, 
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType($"{typeof(T).Name}?", null);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            List<T?> objects = new();
            int index = 0;

            while (true)
            {
                T? serializedObj = SerializeNullable<T>(default, name: $"{name ?? DefaultName}[{index}]");

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

        public override T[] SerializeObjectArrayUntil<T>(
            T?[]? obj,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            Action<T, int>? onPreSerialize = null,
            string? name = null)
            where T : class
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType($"Object[]: {typeof(T)}", null);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            List<T> objects = new();
            int index = 0;

            while (true)
            {
                T serializedObj = SerializeObject<T>(
                    obj: default, 
                    // ReSharper disable once AccessToModifiedClosure
                    onPreSerialize: onPreSerialize == null ? (Action<T>?)null : x => onPreSerialize(x, index), 
                    name: $"{name ?? DefaultName}[{index}]");

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

        public override Pointer?[] SerializePointerArrayUntil(
            Pointer?[]? obj, 
            Func<Pointer?, bool> conditionCheckFunc, 
            Func<Pointer?>? getLastObjFunc = null,
            PointerSize size = PointerSize.Pointer32, 
            Pointer? anchor = null, 
            bool allowInvalid = false, 
            long? nullValue = null,
            string? name = null)
        {
            if (IsSerializerLogEnabled)
            {
                WriteLogPrefix();
                WriteLogDataType(size.ToString(), null);
                WriteLogDataName(name, hasValue: false);
                WriteLogLineEnd();
            }

            List<Pointer?> pointers = new();
            int index = 0;

            while (true)
            {
                Pointer? serializedObj = SerializePointer(
                    obj: default,
                    size: size,
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue,
                    name: $"{name ?? DefaultName}[{index}]");

                index++;

                if (conditionCheckFunc(serializedObj))
                {
                    if (getLastObjFunc == null)
                        pointers.Add(serializedObj);

                    break;
                }

                pointers.Add(serializedObj);
            }

            return pointers.ToArray();
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action)
        {
            if (action == null) 
                throw new ArgumentNullException(nameof(action));
            
            VerifyHasCurrentPointer();
            
            Reader r = Reader;
            bool isLittleEndian = r.IsLittleEndian;
            
            if (isLittleEndian != (endianness == Endian.Little))
            {
                r.IsLittleEndian = endianness == Endian.Little;
                action();
                r.IsLittleEndian = isLittleEndian;
            }
            else
            {
                action();
            }
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        public override void SerializeBitValues(Action<SerializeBits64> serializeFunc)
        {
            if (serializeFunc == null) 
                throw new ArgumentNullException(nameof(serializeFunc));
            
            string? logPrefix = LogPrefix;

            // Extract bits
            int pos = 0;
            
            using MemoryStream buffer = new();
            List<string>? logs = IsSerializerLogEnabled ? new List<string>() : null;

            serializeFunc((_, length, name) => 
            {
                int bitsFromPrevByte = 0;

                if (pos % 8 != 0)
                    bitsFromPrevByte = 8 - pos % 8;

                int bitsToRead = length - bitsFromPrevByte;
                int bytesToRead = (int)Math.Ceiling(bitsToRead / 8f);

                for (int i = 0; i < bytesToRead; i++)
                    buffer.WriteByte(Serialize<byte>(default, name: IsSerializerLogEnabled ? $"Value[{buffer.Length}]" : null));

                long bitValue = BitHelpers.ExtractBits64(buffer.GetBuffer(), length, pos);

                if (IsSerializerLogEnabled)
                    logs?.Add($"{logPrefix}  (UInt{length}) {name ?? DefaultName}: {bitValue}");

                pos += length;
                return bitValue;
            });

            if (IsSerializerLogEnabled && logs != null)
                foreach (string l in logs)
                    SerializerLog.WriteLine(l);
        }

        public override void DoBits<T>(Action<BitSerializerObject> serializeFunc) 
        {
            if (serializeFunc == null) 
                throw new ArgumentNullException(nameof(serializeFunc));
            
            string? logPrefix = LogPrefix;
            Pointer p = CurrentPointer;
            long value = Convert.ToInt64(Serialize<T>(default, name: "Value"));
            serializeFunc(new BitDeserializer(this, p, logPrefix, value));
        }

        #endregion

        #region Caching

        public override Task FillCacheForReadAsync(long length)
        {
            VerifyHasCurrentPointer();
            return FileManager.FillCacheForReadAsync(length, Reader);
        }

        #endregion

        #region Protected Helpers

        [MemberNotNull(nameof(CurrentFile), nameof(Reader))]
        protected void VerifyHasCurrentPointer()
        {
            if (!HasCurrentPointer)
                throw new SerializerMissingCurrentPointerException();
        }

        [MemberNotNull(nameof(CurrentFile), nameof(Reader))]
        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null) 
                throw new ArgumentNullException(nameof(newFile));

            if (!Readers.ContainsKey(newFile))
            {
                Readers.Add(newFile, newFile.CreateReader());
                newFile.InitFileReadMap(Readers[newFile].BaseStream.Length);
            }

            Reader = Readers[newFile];
            CurrentFile = newFile;
        }

        // Helper method which returns an object so we can cast it
        protected object ReadAsObject(Type type, string? name = null)
        {
            VerifyHasCurrentPointer();

            TypeCode typeCode = Type.GetTypeCode(type);

            switch (typeCode)
            {
                case TypeCode.Boolean:
                    byte b = Reader.ReadByte();

                    if (b != 0 && b != 1)
                    {
                        SystemLog?.LogWarning("Binary boolean '{0}' ({1}) was not correctly formatted", name, b);

                        if (IsSerializerLogEnabled)
                        {
                            WriteLogPrefix(CurrentPointer - 1);
                            SerializerLog.WriteLine($"({type}): Binary boolean was not correctly formatted ({b})");
                        }
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

                case TypeCode.Object when type == typeof(UInt24):
                    return Reader.ReadUInt24();

                case TypeCode.String:
                case TypeCode.Decimal:
                case TypeCode.Char:
                case TypeCode.DateTime:
                case TypeCode.Empty:
                case TypeCode.DBNull:
                default:
                    throw new NotSupportedException($"The specified generic type ('{name}') can not be read from the reader");
            }
        }

        protected object? ReadAsNullableObject(Type underlyingType, string? name = null)
        {
            VerifyHasCurrentPointer();

            TypeCode typeCode = Type.GetTypeCode(underlyingType);

            return typeCode switch
            {
                TypeCode.SByte => toNullable(Reader.ReadSByte(), -1),
                TypeCode.Byte => toNullable(Reader.ReadByte(), Byte.MaxValue),
                TypeCode.Int16 => toNullable(Reader.ReadInt16(), -1),
                TypeCode.UInt16 => toNullable(Reader.ReadUInt16(), UInt16.MaxValue),
                TypeCode.Int32 => toNullable(Reader.ReadInt32(), -1),
                TypeCode.UInt32 => toNullable(Reader.ReadUInt32(), UInt32.MaxValue),
                TypeCode.Int64 => toNullable(Reader.ReadInt64(), -1),
                TypeCode.UInt64 => toNullable(Reader.ReadUInt64(), UInt64.MaxValue),
                TypeCode.Object when underlyingType == typeof(UInt24) => toNullable(Reader.ReadUInt24(), UInt24.MaxValue),
                _ => throw new NotSupportedException($"The specified nullable generic type ('{name}') can not be read from the reader")
            };

            T? toNullable<T>(T value, T nullValue) where T : struct => value.Equals(nullValue) ? null : value;
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

        public void DisposeFile(BinaryFile? file)
        {
            if (file == null || !Readers.ContainsKey(file))
                return;

            Reader r = Readers[file];
            file.EndRead(r);
            Readers.Remove(file);

            if (CurrentFile == file)
                CurrentFile = null;

            SystemLog?.LogTrace("Disposed file from deserializer {0}", file.FilePath);
        }

        #endregion
    }
}