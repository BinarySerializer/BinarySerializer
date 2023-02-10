#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// A binary serializer used for serializing
    /// </summary>
    public class BinarySerializer : SerializerObject, IDisposable
    {
        #region Constructor

        public BinarySerializer(Context context) : base(context)
        {
            Writers = new Dictionary<BinaryFile, Writer>();
            WrittenObjects = new HashSet<BinarySerializable>(new IdentityComparer<BinarySerializable>());
        }

        #endregion

        #region Protected Properties

        protected HashSet<BinarySerializable> WrittenObjects { get; }
        protected Dictionary<BinaryFile, Writer> Writers { get; }
        protected Writer? Writer { get; set; }
        protected BinaryFile? CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => Writer?.BaseStream.Length ?? throw new SerializerMissingCurrentPointerException();

        [MemberNotNullWhen(true, nameof(CurrentFile), nameof(Writer))]
        public override bool HasCurrentPointer => CurrentFile != null && Writer != null;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile ?? throw new SerializerMissingCurrentPointerException();

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => Writer?.BaseStream.Position ?? throw new SerializerMissingCurrentPointerException();

        #endregion

        #region Logging

        protected string? LogPrefix => IsSerializerLoggerEnabled ? $"(W) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}" : null;
        public override void Log(string logString, params object?[] args)
        {
            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log(LogPrefix + String.Format(logString, args));
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
         
            VerifyHasCurrentPointer();
            
            if (encoder == null)
            {
                action();
                return;
            }

            using MemoryStream decodedStream = new();
            
            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Create a temporary file for the stream to serialize to
            StreamFile sf = new(
                context: Context,
                name: key,
                stream: decodedStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: CurrentPointer);

            try
            {
                // Add the temporary file
                Context.AddFile(sf);

                // Serialize the data into the stream
                DoAt(sf.StartPointer, action);

                // Encode the stream to the current file
                decodedStream.Position = 0;
                encoder.EncodeStream(decodedStream, Writer.BaseStream);
            }
            finally
            {
                // Remove the temporary file
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

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Add the stream
            MemoryStream memStream = new();

            StreamFile sf = new(
                context: Context,
                name: key,
                stream: memStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: CurrentPointer);

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

            VerifyHasCurrentPointer();

            EncodedFiles.Remove(encodedFile);

            encodedFile.Stream.Position = 0;
            encodedFile.Encoder.EncodeStream(encodedFile.Stream, Writer.BaseStream);
            encodedFile.Stream.Close();
            Context.RemoveFile(encodedFile.File);
        }

        #endregion

        #region XOR

        public override void BeginXOR(IXORCalculator? xorCalculator)
        {
            VerifyHasCurrentPointer();
            Writer.BeginXOR(xorCalculator);
        }

        public override void EndXOR()
        {
            VerifyHasCurrentPointer();
            Writer.EndXOR();
        }

        public override IXORCalculator? GetXOR()
        {
            VerifyHasCurrentPointer();
            return Writer.GetXORCalculator();
        }

        #endregion

        #region Positioning

        public override void Goto(Pointer? offset)
        {
            if (offset == null) 
                return;

            if (offset.File != CurrentFile || !HasCurrentPointer)
                SwitchToFile(offset.File);

            Writer.BaseStream.Position = offset.FileOffset;
        }

        public override void Align(int alignBytes = 4, Pointer? baseOffset = null, bool? logIfNotNull = null)
        {
            long align = (CurrentAbsoluteOffset - (baseOffset?.AbsoluteOffset ?? 0)) % alignBytes;

            // Make sure we need to align
            if (align == 0)
                return;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}Align {alignBytes}");

            long count = alignBytes - align;

            // We can ignore logIfNotNull here since we're writing
            Goto(CurrentPointer + count);
        }

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string? name = null)
        {
            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({typeof(T)}) {name ?? DefaultName}: {calculatedChecksum}");

            Write(calculatedChecksum);
            return calculatedChecksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator? checksumCalculator)
        {
            VerifyHasCurrentPointer();
            Writer.BeginCalculateChecksum(checksumCalculator);
        }

        public override IChecksumCalculator? PauseCalculateChecksum()
        {
            VerifyHasCurrentPointer();
            return Writer.PauseCalculateChecksum();
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
            return Writer.EndCalculateChecksum<T>();
        }

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string? name = null)
        {
            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({typeof(T).Name}) {name ?? DefaultName}: {obj}");

            Write(obj);

            return obj;
        }

        public override T? SerializeNullable<T>(T? obj, string? name = null)
        {
            Type type = typeof(T);

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({type.Name}?) {name ?? DefaultName}: {obj}");

            WriteNullable(type, obj);

            return obj;
        }

        public override T SerializeObject<T>(T? obj, Action<T>? onPreSerialize = null, string? name = null)
            where T : class
        {
            if (obj == null) 
            {
                obj = new T();
                obj.Init(CurrentPointer);
            }
            else if (WrittenObjects.Contains(obj))
            {
                Goto(CurrentPointer + obj.SerializedSize);
                return obj;
            }
            else
            {
                // Reinitialize object
                obj.Init(CurrentPointer);
            }

            string? logString = LogPrefix;
            bool isLogTemporarilyDisabled = false;

            if (!DisableSerializerLogForObject && obj is ISerializerShortLog)
            {
                DisableSerializerLogForObject = true;
                isLogTemporarilyDisabled = true;
            }

            if (IsSerializerLoggerEnabled) 
                Context.SerializerLogger.Log($"{logString}(Object: {typeof(T)}) {name ?? DefaultName}");

            try
            {
                Depth++;
                onPreSerialize?.Invoke(obj);
                obj.Serialize(this);
            }
            finally
            {
                Depth--;

                if (isLogTemporarilyDisabled)
                {
                    DisableSerializerLogForObject = false;
                    if (IsSerializerLoggerEnabled)
                    {
                        string shortLog = (obj as ISerializerShortLog)?.ShortLog ?? "null";
                        Context.SerializerLogger.Log($"{logString}({typeof(T)}) {name ?? DefaultName}: {shortLog}");
                    }
                }
            }

            WrittenObjects.Add(obj);

            return obj;
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

            // Redirect pointer files for pointer which have removed files, such as if the pointer is serialized within encoded data
            if (obj != null && !Context.FileExists(obj.File))
            {
                obj = CurrentBinaryFile.FileRedirectBehavior switch
                {
                    BinaryFile.RedirectBehavior.Throw => 
                        throw new ContextException($"The file for the pointer {obj} does not exist in the current context"),
                    
                    BinaryFile.RedirectBehavior.CurrentFile => 
                        new Pointer(obj.AbsoluteOffset, CurrentBinaryFile, obj.Anchor, obj.Size, Pointer.OffsetType.Absolute),
                    
                    BinaryFile.RedirectBehavior.SpecifiedFile when CurrentBinaryFile.RedirectFile != null => 
                        new Pointer(
                            offset: obj.AbsoluteOffset, 
                            file: CurrentBinaryFile.RedirectFile, 
                            anchor: obj.Anchor, 
                            size: obj.Size, 
                            offsetType: Pointer.OffsetType.Absolute),
                    
                    _ => obj
                };
            }

            BinaryFile currentFile = CurrentFile;

            if (Defaults != null)
            {
                if (anchor == null) 
                    anchor = Defaults.PointerAnchor;

                if (Defaults.PointerFile != null)
                    currentFile = Defaults.PointerFile;

                nullValue ??= Defaults.PointerNullValue;
            }

            if (anchor != null && obj != null)
                obj = new Pointer(obj.SerializedOffset, obj.File, anchor, obj.Size);

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({size}) {name ?? DefaultName}: {obj}");

            long valueToSerialize = currentFile.GetPointerValueToSerialize(obj, anchor: anchor, nullValue: nullValue);

            switch (size)
            {
                case PointerSize.Pointer16:
                    Writer.Write((ushort)valueToSerialize);
                    break;

                case PointerSize.Pointer32:
                    Writer.Write((uint)valueToSerialize);
                    break;

                case PointerSize.Pointer64:
                    Writer.Write((long)valueToSerialize);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }

            return obj;
        }

        public override Pointer<T> SerializePointer<T>(
            Pointer<T>? obj,
            PointerSize size = PointerSize.Pointer32,
            Pointer? anchor = null,
            bool allowInvalid = false,
            long? nullValue = null,
            string? name = null)
        {
            obj ??= new Pointer<T>();

            Pointer? pointerValue = obj.PointerValue;
            pointerValue = SerializePointer(
                obj: pointerValue, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: IsSerializerLoggerEnabled ? $"<{typeof(T)}> {name ?? DefaultName}" : name);
            
            if (obj.PointerValue != pointerValue)
                obj = new Pointer<T>(pointerValue, obj.Value);

            return obj;
        }

        public override string SerializeString(string? obj, long? length = null, Encoding? encoding = null, string? name = null)
        {
            VerifyHasCurrentPointer();

            obj ??= String.Empty;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}(String) {name ?? DefaultName}: {obj}");

            encoding ??= Defaults?.StringEncoding ?? Context.DefaultEncoding;

            if (length.HasValue)
                Writer.WriteString(obj, length.Value, encoding);
            else
                Writer.WriteNullDelimitedString(obj, encoding);

            return obj;
        }

        #endregion

        #region Array Serialization

        public override T?[] SerializeArraySize<T, U>(T?[]? obj, string? name = null)
            where T : default
        {
            obj ??= Array.Empty<T?>();
            U size = (U)Convert.ChangeType(obj.Length, typeof(U));
            Serialize<U>(size, name: $"{name ?? DefaultName}.Length");
            return obj;
        }

        public override T[] SerializeArray<T>(T[]? obj, long count, string? name = null)
        {
            VerifyHasCurrentPointer();

            T[] buffer = obj ?? new T[count];
            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
            {
                if (typeof(T) == typeof(byte))
                {
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}: ";
                    string hexStr = ((byte[])(object)buffer).ToHexString(
                        align: 16, 
                        newLinePrefix: new string(' ', normalLog.Length), 
                        maxLines: 10);
                    Context.SerializerLogger.Log(normalLog + hexStr);
                }
                else
                {
                    Context.SerializerLogger.Log($"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}");
                }
            }

            // Use byte writing method if requested
            if (typeof(T) == typeof(byte))
            {
                Writer.Write((byte[])(object)buffer);
                return buffer;
            }

            for (int i = 0; i < count; i++)
                buffer[i] = Serialize<T>(buffer[i], name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer;
        }

        public override T?[] SerializeNullableArray<T>(T?[]? obj, long count, string? name = null)
        {
            VerifyHasCurrentPointer();

            T?[] buffer = obj ?? new T?[count];
            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                buffer[i] = SerializeNullable<T>(buffer[i], name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T?[]? obj, long count, Action<T, int>? onPreSerialize = null, string? name = null)
            where T : class
        {
            T?[] buffer = obj ?? new T?[count];
            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}(Object[] {typeof(T)}[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                buffer[i] = SerializeObject<T>(
                    obj: buffer[i], 
                    // ReSharper disable once AccessToModifiedClosure
                    onPreSerialize: onPreSerialize == null ? (Action<T>?)null : x => onPreSerialize(x, i), 
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

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
            Pointer?[] buffer = obj ?? new Pointer?[count];

            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}({size}[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                buffer[i] = SerializePointer(
                    obj: buffer[i],
                    size: size,
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue,
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

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
            Pointer<T>?[] buffer = obj ?? new Pointer<T>?[count];
            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log($"{LogPrefix}(Pointer<{typeof(T)}>[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                buffer[i] = SerializePointer<T>(
                    obj: buffer[i],
                    size: size,
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue,
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer!;
        }

        public override string[] SerializeStringArray(
            string?[]? obj,
            long count,
            long? length = null,
            Encoding? encoding = null,
            string? name = null)
        {
            string?[] buffer = obj ?? new string?[count];
            count = buffer.Length;

            if (IsSerializerLoggerEnabled)
                Context.SerializerLogger.Log(LogPrefix + "(String[" + count + "]) " + (name ?? DefaultName));

            for (int i = 0; i < count; i++)
                buffer[i] = SerializeString(
                    obj: buffer[i], 
                    length, encoding, 
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[{i}]" : name);

            return buffer!;
        }

        public override T[] SerializeArrayUntil<T>(
            T[]? obj,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            string? name = null)
        {
            obj ??= Array.Empty<T>();

            // Serialize the array
            obj = SerializeArray<T>(obj, obj.Length, name: name);

            // Serialize the terminator value if there is one
            if (getLastObjFunc != null)
                Serialize<T>(getLastObjFunc(), name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[x]" : name);

            return obj;
        }

        public override T?[] SerializeNullableArrayUntil<T>(
            T?[]? obj, 
            Func<T?, bool> conditionCheckFunc, 
            Func<T?>? getLastObjFunc = null, 
            string? name = null)
        {
            obj ??= Array.Empty<T?>();

            // Serialize the array
            obj = SerializeNullableArray<T>(obj, obj.Length, name: name);

            // Serialize the terminator value if there is one
            if (getLastObjFunc != null)
                SerializeNullable<T>(getLastObjFunc(), name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[x]" : name);

            return obj;
        }

        public override T[] SerializeObjectArrayUntil<T>(
            T?[]? obj,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            Action<T, int>? onPreSerialize = null,
            string? name = null)
            where T : class
        {
            obj ??= Array.Empty<T?>();

            // Serialize the array
            obj = SerializeObjectArray<T>(obj, obj.Length, onPreSerialize: onPreSerialize, name: name);

            // Serialize the terminator object if there is one
            if (getLastObjFunc != null)
                SerializeObject<T>(
                    obj: getLastObjFunc(), 
                    onPreSerialize: onPreSerialize != null ? x => onPreSerialize(x, obj.Length) : null, 
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[x]" : name);

            return obj!;
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
            obj ??= Array.Empty<Pointer?>();

            // Serialize the array
            obj = SerializePointerArray(
                obj: obj, 
                count: obj.Length, 
                size: size, 
                anchor: anchor, 
                allowInvalid: allowInvalid, 
                nullValue: nullValue, 
                name: name);

            // Serialize the terminator pointer if there is one
            if (getLastObjFunc != null)
                SerializePointer(
                    obj: getLastObjFunc(),
                    size: size,
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue,
                    name: IsSerializerLoggerEnabled ? $"{name ?? DefaultName}[x]" : name);

            return obj;
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action)
        {
            if (action == null) 
                throw new ArgumentNullException(nameof(action));
            
            VerifyHasCurrentPointer();

            Writer w = Writer;
            bool isLittleEndian = w.IsLittleEndian;
            
            if (isLittleEndian != (endianness == Endian.Little))
            {
                w.IsLittleEndian = endianness == Endian.Little;
                action();
                w.IsLittleEndian = isLittleEndian;
            }
            else
            {
                action();
            }
        }

        public override void SerializeBitValues(Action<SerializeBits64> serializeFunc) => throw new NotImplementedException();

        public override void DoBits<T>(Action<BitSerializerObject> serializeFunc) 
        {
            if (serializeFunc == null)
                throw new ArgumentNullException(nameof(serializeFunc));

            BitSerializer serializer = new(this, CurrentPointer, LogPrefix, 0);

            // Set bits
            serializeFunc(serializer);

            // Serialize value
            Serialize<T>((T)Convert.ChangeType(serializer.Value, typeof(T)), name: "Value");
        }

        #endregion

        #region Public Helpers

        public void ClearWrittenObjects() => WrittenObjects.Clear();

        #endregion

        #region Protected Helpers

        [MemberNotNull(nameof(CurrentFile), nameof(Writer))]
        protected void VerifyHasCurrentPointer()
        {
            if (!HasCurrentPointer)
                throw new SerializerMissingCurrentPointerException();
        }

        /// <summary>
        /// Writes a supported value to the stream
        /// </summary>
        /// <param name="value">The value</param>
        protected void Write(object value)
        {
            if (value == null) 
                throw new ArgumentNullException(nameof(value));
            
            VerifyHasCurrentPointer();

            Type type = value.GetType();

            if (type.IsEnum)
                Write(Convert.ChangeType(value, Enum.GetUnderlyingType(type)));

            else if (value is bool bo)
                Writer.Write((byte)(bo ? 1 : 0));

            else if (value is sbyte sb)
                Writer.Write((byte)sb);

            else if (value is byte by)
                Writer.Write(by);

            else if (value is short sh)
                Writer.Write(sh);

            else if (value is ushort ush)
                Writer.Write(ush);

            else if (value is int i32)
                Writer.Write(i32);

            else if (value is uint ui32)
                Writer.Write(ui32);

            else if (value is long lo)
                Writer.Write(lo);

            else if (value is ulong ulo)
                Writer.Write(ulo);

            else if (value is float fl)
                Writer.Write(fl);

            else if (value is double dou)
                Writer.Write(dou);

            else if (value is UInt24 u24)
                Writer.Write(u24);

            else
                throw new NotSupportedException($"The specified type {type.Name} is not supported.");
        }

        protected void WriteNullable(Type type, object? value)
        {
            VerifyHasCurrentPointer();

            if (type.IsEnum)
                Write(Convert.ChangeType(value, Enum.GetUnderlyingType(type)));

            else if (type == typeof(sbyte))
                Writer.Write((byte)((sbyte?)value ?? -1));

            else if (type == typeof(byte))
                Writer.Write((byte?)value ?? Byte.MaxValue);

            else if (type == typeof(short))
                Writer.Write((short?)value ?? -1);

            else if (type == typeof(ushort))
                Writer.Write((ushort?)value ?? UInt16.MaxValue);

            else if (type == typeof(int))
                Writer.Write((int?)value ?? -1);

            else if (type == typeof(uint))
                Writer.Write((uint?)value ?? UInt32.MaxValue);

            else if (type == typeof(long))
                Writer.Write((long?)value ?? -1);

            else if (type == typeof(ulong))
                Writer.Write((ulong?)value ?? UInt64.MaxValue);

            else if (type == typeof(UInt24))
                Writer.Write((UInt24?)value ?? UInt24.MaxValue);

            else
                throw new NotSupportedException($"The specified nullable type {type.Name} is not supported.");
        }

        [MemberNotNull(nameof(CurrentFile), nameof(Writer))]
        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null)
                throw new ArgumentNullException(nameof(newFile));

            if (!Writers.ContainsKey(newFile))
                Writers.Add(newFile, newFile.CreateWriter());

            Writer = Writers[newFile];
            CurrentFile = newFile;
        }

        #endregion

        #region Disposing

        public void Dispose()
        {
            foreach (KeyValuePair<BinaryFile, Writer> w in Writers)
                w.Key.EndWrite(w.Value);

            Writers.Clear();
            Writer = null;
        }

        public void DisposeFile(BinaryFile? file)
        {
            if (file == null || !Writers.ContainsKey(file)) 
                return;

            Writer w = Writers[file];
            file.EndWrite(w);
            Writers.Remove(file);

            if (CurrentFile == file)
                CurrentFile = null;

            SystemLogger?.LogTrace("Disposed file from serializer {0}", file.FilePath);
        }

        #endregion

        #region Data Types

        private sealed class IdentityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            public bool Equals(T x, T y) => ReferenceEquals(x, y);
            public int GetHashCode(T obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        #endregion
    }
}