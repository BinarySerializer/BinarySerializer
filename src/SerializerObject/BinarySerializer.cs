using System;
using System.Collections.Generic;
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
        protected Writer Writer { get; set; }
        protected BinaryFile CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => Writer.BaseStream.Length; // can be modified!

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile;

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => Writer.BaseStream.Position;

        #endregion

        #region Logging

        protected string LogPrefix => IsSerializerLogEnabled ? $"(W) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}" : null;
        public override void Log(string logString, params object[] args)
        {
            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log(LogPrefix + String.Format(logString, args));
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

        public override Pointer BeginEncoded(IStreamEncoder encoder, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            if (encoder == null)
                throw new ArgumentNullException(nameof(encoder));

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Add the stream
            MemoryStream memStream = new MemoryStream();

            StreamFile sf = new StreamFile(
                context: Context,
                name: key,
                stream: memStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: CurrentPointer);

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

            encodedFile.Stream.Position = 0;
            encodedFile.Encoder.EncodeStream(encodedFile.Stream, Writer.BaseStream);
            encodedFile.Stream.Close();
            Context.RemoveFile(encodedFile.File);
        }

        #endregion

        #region XOR

        public override void BeginXOR(IXORCalculator xorCalculator) => Writer.BeginXOR(xorCalculator);
        public override void EndXOR() => Writer.EndXOR();
        public override IXORCalculator GetXOR() => Writer.GetXORCalculator();

        #endregion

        #region Positioning

        public override void Goto(Pointer offset)
        {
            if (offset == null) 
                return;

            if (offset.File != CurrentFile)
                SwitchToFile(offset.File);

            Writer.BaseStream.Position = offset.FileOffset;
        }

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string name = null)
        {
            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}({typeof(T)}) {name ?? DefaultName}: {calculatedChecksum}");

            Write(calculatedChecksum);
            return calculatedChecksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => Writer.BeginCalculateChecksum(checksumCalculator);

        public override IChecksumCalculator PauseCalculateChecksum() => Writer.PauseCalculateChecksum();

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <returns>The checksum value</returns>
        public override T EndCalculateChecksum<T>() => Writer.EndCalculateChecksum<T>();

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string name = null)
        {
            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}({typeof(T).Name}) {(name ?? DefaultName)}: {obj?.ToString() ?? "null"}");

            Write(obj);
            return obj;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null)
        {
            if (obj == null) 
            {
                obj = new T();
                obj.Init(CurrentPointer);
            }

            if (WrittenObjects.Contains(obj))
            {
                Goto(CurrentPointer + obj.Size);
                return obj;
            }

            // reinitialize object
            obj.Init(CurrentPointer);

            string logString = IsSerializerLogEnabled ? LogPrefix : null;
            bool isLogTemporarilyDisabled = false;

            if (!DisableSerializerLogForObject && obj.UseShortLog)
            {
                DisableSerializerLogForObject = true;
                isLogTemporarilyDisabled = true;
            }

            if (IsSerializerLogEnabled) 
                Context.SerializerLog.Log($"{logString}(Object: {typeof(T)}) {name ?? DefaultName}");

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
                    if (IsSerializerLogEnabled)
                        Context.SerializerLog.Log($"{logString}({typeof(T)}) {name ?? DefaultName}: {obj.ShortLog}");
                }
            }

            WrittenObjects.Add(obj);

            return obj;
        }

        public override Pointer SerializePointer(Pointer obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            // Redirect pointer files for pointer which have removed files, such as if the pointer is serialized within encoded data
            if (obj != null && !Context.FileExists(obj.File))
            {
                obj = CurrentBinaryFile.FileRedirectBehavior switch
                {
                    BinaryFile.RedirectBehavior.Throw => throw new ContextException($"The file for the pointer {obj} does not exist in the current context"),
                    BinaryFile.RedirectBehavior.CurrentFile => new Pointer(obj.AbsoluteOffset, CurrentBinaryFile, obj.Anchor, obj.Size, Pointer.OffsetType.Absolute),
                    BinaryFile.RedirectBehavior.SpecifiedFile => new Pointer(obj.AbsoluteOffset, CurrentBinaryFile.RedirectFile, obj.Anchor, obj.Size, Pointer.OffsetType.Absolute),
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

            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}({size}) {name ?? DefaultName}: {obj}");

            var valueToSerialize = currentFile.GetPointerValueToSerialize(obj, anchor: anchor, nullValue: nullValue);

            switch (size)
            {
                case PointerSize.Pointer16:
                    Write((ushort)valueToSerialize);
                    break;

                case PointerSize.Pointer32:
                    Write((uint)valueToSerialize);
                    break;

                case PointerSize.Pointer64:
                    Write((long)valueToSerialize);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }

            return obj;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer PointerValue = obj?.PointerValue;
            PointerValue = SerializePointer(PointerValue, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: IsSerializerLogEnabled ? $"<{typeof(T)}> {name ?? DefaultName}" : name);
            if (obj?.PointerValue != PointerValue) {
                obj = new Pointer<T>(PointerValue, obj == null ? default : obj.Value);
            }
            return obj;
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null)
        {
            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}(String) {name ?? DefaultName}: {obj}");

            encoding ??= Defaults?.StringEncoding ?? Context.DefaultEncoding;

            if (length.HasValue)
                Writer.WriteString(obj, length.Value, encoding);
            else
                Writer.WriteNullDelimitedString(obj, encoding);

            return obj;
        }

        #endregion

        #region Array Serialization

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null)
        {
            U Size = (U)Convert.ChangeType(obj?.Length ?? 0, typeof(U));
            Serialize<U>(Size, name: $"{name ?? DefaultName}.Length");
            return obj;
        }

        public override T[] SerializeArray<T>(T[] obj, long count, string name = null)
        {
            T[] buffer = GetArray(obj, count);
            count = buffer.Length;

            if (IsSerializerLogEnabled)
            {
                if (typeof(T) == typeof(byte))
                {
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}: ";
                    Context.SerializerLog.Log(normalLog + 
                                    ((byte[])(object)buffer).ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
                }
                else
                {
                    Context.SerializerLog.Log($"{LogPrefix}({typeof(T).Name}[{count}]) {name ?? DefaultName}");
                }
            }

            // Use byte writing method if requested
            if (typeof(T) == typeof(byte))
            {
                Writer.Write((byte[])(object)buffer);
                return buffer;
            }

            for (int i = 0; i < count; i++)
                // Read the value
                Serialize<T>(buffer[i], name: (name == null || !IsSerializerLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T, int> onPreSerialize = null, string name = null)
        {
            T[] buffer = GetArray(obj, count);
            count = buffer.Length;

            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}(Object[] {typeof(T)}[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                // Read the value
                SerializeObject<T>(
                    obj: buffer[i], 
                    onPreSerialize: onPreSerialize == null ? (Action<T>)null : x => onPreSerialize(x, i), 
                    name: name == null || !IsSerializerLogEnabled ? name : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null, string name = null)
        {
            T[] array = obj;

            if (getLastObjFunc != null)
                array = array.Append(getLastObjFunc()).ToArray();

            SerializeArray<T>(array, array.Length, name: name);

            return obj;
        }

        public override T[] SerializeObjectArrayUntil<T>(T[] obj, Func<T, bool> conditionCheckFunc, Func<T> getLastObjFunc = null,
            Action<T, int> onPreSerialize = null, string name = null)
        {
            T[] array = obj;

            if (getLastObjFunc != null)
                array = array.Append(getLastObjFunc()).ToArray();

            SerializeObjectArray<T>(array, array.Length, onPreSerialize: onPreSerialize, name: name);

            return obj;
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer[] buffer = GetArray(obj, count);
            count = buffer.Length;

            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}({size}[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer(buffer[i], size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: (name == null || !IsSerializerLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer<T>[] buffer = GetArray(obj, count);
            count = buffer.Length;

            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log($"{LogPrefix}(Pointer<{typeof(T)}>[{count}]) {name ?? DefaultName}");

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer<T>(
                    obj: buffer[i], 
                    size: size, 
                    anchor: anchor, 
                    allowInvalid: allowInvalid, 
                    nullValue: nullValue, 
                    name: name == null || !IsSerializerLogEnabled ? name : $"{name}[{i}]");

            return buffer;
        }

        public override string[] SerializeStringArray(string[] obj, long count, long? length = null, Encoding encoding = null, string name = null)
        {
            string[] buffer = GetArray(obj, count);
            count = buffer.Length;

            if (IsSerializerLogEnabled)
                Context.SerializerLog.Log(LogPrefix + "(String[" + count + "]) " + (name ?? DefaultName));

            for (int i = 0; i < count; i++)
                // Read the value
                SerializeString(buffer[i], length, encoding, name: (name == null || !IsSerializerLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action)
        {
            Writer w = Writer;
            bool isLittleEndian = w.IsLittleEndian;
            if (isLittleEndian != (endianness == Endian.Little))
            {
                w.IsLittleEndian = (endianness == Endian.Little);
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
            var serializer = new BitSerializer(this, CurrentPointer, LogPrefix, 0);

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

        protected T[] GetArray<T>(T[] obj, long count)
        {
            // Create or resize array if necessary
            return obj ?? new T[(int)count];
        }

        /// <summary>
        /// Writes a supported value to the stream
        /// </summary>
        /// <param name="value">The value</param>
        protected void Write<T>(T value)
        {
            if (value is byte[] ba)
                Writer.Write(ba);

            else if (value is Array a)
                foreach (var item in a)
                    Write(item);

            else if (value?.GetType().IsEnum == true)
                Write(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())));

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

            else if (value is string s)
                Writer.WriteNullDelimitedString(s, Defaults?.StringEncoding ?? Context.DefaultEncoding);

            else if (value is UInt24 u24)
                Writer.Write(u24);

            else if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                // It's nullable
                Type underlyingType = Nullable.GetUnderlyingType(typeof(T));
                if (underlyingType == typeof(byte))
                {
                    var v = (byte?)(object)value;
                    
                    if (v.HasValue)
                        Writer.Write(v.Value);
                    else
                        Writer.Write((byte)0xFF);
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

        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null)
                return;

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

        public void DisposeFile(BinaryFile file)
        {
            if (!Writers.ContainsKey(file)) 
                return;

            Writer w = Writers[file];
            file.EndWrite(w);
            Writers.Remove(file);

            if (CurrentFile == file)
                CurrentFile = null;

            SystemLog?.LogTrace("Disposed file from serializer {0}", file.FilePath);
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