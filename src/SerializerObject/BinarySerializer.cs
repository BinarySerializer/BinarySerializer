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

        protected string LogPrefix => IsLogEnabled ? ($"(W) {CurrentPointer}:{new string(' ', (Depth + 1) * 2)}") : null;
        public override void Log(string logString)
        {
            if (IsLogEnabled)
                Context.Log.Log(LogPrefix + logString);
        }

        #endregion

        #region Encoding

        public override void DoEncoded(IStreamEncoder encoder, Action action, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
            // Encode the data into a stream
            Stream encoded = null;

            try
            {
                using (MemoryStream memStream = new MemoryStream())
                {
                    // Stream key
                    string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

                    // Add the stream
                    StreamFile sf = new StreamFile(
                        context: Context,
                        name: key,
                        stream: memStream,
                        endianness: endianness ?? CurrentFile.Endianness,
                        allowLocalPointers: allowLocalPointers,
                        parentPointer: CurrentPointer);

                    try
                    {
                        Context.AddFile(sf);

                        DoAt(sf.StartPointer, () =>
                        {
                            action();
                            memStream.Position = 0;
                            encoded = encoder.EncodeStream(memStream);
                        });
                    }
                    finally
                    {
                        Context.RemoveFile(sf);
                    }
                }
                // Turn stream into array & write bytes
                if (encoded != null)
                {
                    using MemoryStream ms = new MemoryStream();
                    encoded.CopyTo(ms);
                    Writer.Write(ms.ToArray());
                }
            }
            finally
            {
                encoded?.Dispose();
            }
        }

        public override Pointer BeginEncoded(IStreamEncoder encoder, Endian? endianness = null, bool allowLocalPointers = false, string filename = null)
        {
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
            var encodedFile = EncodedFiles.FirstOrDefault(ef => ef.File == endPointer.File);
            if (encodedFile != null)
            {
                EncodedFiles.Remove(encodedFile);

                encodedFile.Stream.Position = 0;
                Stream encoded = null;
                encoded = encodedFile.Encoder.EncodeStream(encodedFile.Stream);
                encodedFile.Stream.Close();
                Context.RemoveFile(encodedFile.File);

                // Turn stream into array & write bytes
                if (encoded != null)
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        encoded.CopyTo(ms);
                        Writer.Write(ms.ToArray());
                    }
                    encoded.Close();
                }
            }
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
            if (offset == null) return;
            if (offset.File != CurrentFile)
            {
                SwitchToFile(offset.File);
            }
            Writer.BaseStream.Position = offset.FileOffset;
        }

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string name = null)
        {
            if (IsLogEnabled)
            {
                Context.Log.Log($"{LogPrefix}({typeof(T)}) {(name ?? "<no name>")}: {calculatedChecksum}");
            }
            Write(calculatedChecksum);
            return calculatedChecksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) => Writer.BeginCalculateChecksum(checksumCalculator);

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
            if (IsLogEnabled)
            {
                Context.Log.Log($"{LogPrefix}({typeof(T).Name}) {(name ?? "<no name>")}: {(obj?.ToString() ?? "null")}");
            }
            Write(obj);
            return obj;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null)
        {
            if (obj == null) {
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

            string logString = IsLogEnabled ? LogPrefix : null;
            bool isLogTemporarilyDisabled = false;
            if (!DisableLogForObject && (obj?.UseShortLog ?? false))
            {
                DisableLogForObject = true;
                isLogTemporarilyDisabled = true;
            }
            if (IsLogEnabled) Context.Log.Log($"{logString}(Object: {typeof(T)}) {(name ?? "<no name>")}");

            Depth++;
            onPreSerialize?.Invoke(obj);

            obj.Serialize(this);
            Depth--;

            if (isLogTemporarilyDisabled)
            {
                DisableLogForObject = false;
                if (IsLogEnabled)
                    Context.Log.Log($"{logString}({typeof(T)}) {(name ?? "<no name>")}: {(obj?.ShortLog ?? "null")}");
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
                    BinaryFile.RedirectBehavior.Throw => throw new Exception($"The file for the pointer {obj} does not exist in the current context"),
                    BinaryFile.RedirectBehavior.CurrentFile => new Pointer(obj.AbsoluteOffset, CurrentBinaryFile, obj.Anchor, obj.Size, Pointer.OffsetType.Absolute),
                    BinaryFile.RedirectBehavior.SpecifiedFile => new Pointer(obj.AbsoluteOffset, CurrentBinaryFile.RedirectFile, obj.Anchor, obj.Size, Pointer.OffsetType.Absolute),
                    _ => obj
                };
            }

            if (anchor != null && obj != null)
                obj = new Pointer(obj.SerializedOffset, obj.File, anchor, obj.Size);

            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}({size}) {name ?? "<no name>"}: {obj}");

            switch (size)
            {
                case PointerSize.Pointer16:
                    Write((ushort)(obj?.SerializedOffset ?? nullValue ?? 0));
                    break;
                case PointerSize.Pointer32:
                    Write((uint)(obj?.SerializedOffset ?? nullValue ?? 0));
                    break;
                case PointerSize.Pointer64:
                    Write((long)(obj?.SerializedOffset ?? nullValue ?? 0));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }

            return obj;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}(Pointer<T>: {typeof(T)}) {(name ?? "<no name>")}");

            Depth++;

            switch (size)
            {
                case PointerSize.Pointer16:
                    Serialize<ushort>((ushort)(obj?.PointerValue?.SerializedOffset ?? nullValue ?? 0), name: nameof(size));
                    break;
                case PointerSize.Pointer32:
                    Serialize<uint>((uint)(obj?.PointerValue?.SerializedOffset ?? nullValue ?? 0), name: nameof(size));
                    break;
                case PointerSize.Pointer64:
                    Serialize<long>(obj?.PointerValue?.SerializedOffset ?? nullValue ?? 0, name: nameof(size));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(size), size, null);
            }

            if (obj?.PointerValue != null && resolve && obj.Value != null)
                DoAt(obj.PointerValue, () => SerializeObject<T>(obj.Value, onPreSerialize: onPreSerialize, name: "Value"));

            Depth--;
            return obj;
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null)
        {
            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}(String) {(name ?? "<no name>")}: {obj}");

            if (length.HasValue)
                Writer.WriteString(obj, length.Value, encoding ?? Context.DefaultEncoding);
            else
                Writer.WriteNullDelimitedString(obj, encoding ?? Context.DefaultEncoding);

            return obj;
        }

        #endregion

        #region Array Serialization

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null)
        {
            U Size = (U)Convert.ChangeType((obj?.Length) ?? 0, typeof(U));
            //U Size = (U)(object)((obj?.Length) ?? 0);
            Serialize<U>(Size, name: name + ".Length");
            return obj;
        }

        public override T[] SerializeArray<T>(T[] obj, long count, string name = null)
        {
            T[] buffer = GetArray(obj, count);

            if (IsLogEnabled)
            {
                if (typeof(T) == typeof(byte))
                {
                    string normalLog = $"{LogPrefix}({typeof(T).Name}[{count}]) {(name ?? "<no name>")}: ";
                    Context.Log.Log(normalLog
                        + ((byte[])(object)buffer).ToHexString(align: 16, newLinePrefix: new string(' ', normalLog.Length), maxLines: 10));
                }
                else
                {
                    Context.Log.Log($"{LogPrefix}({typeof(T).Name}[{count}]) {(name ?? "<no name>")}");
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
                Serialize<T>(buffer[i], name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize = null, string name = null)
        {
            T[] buffer = GetArray(obj, count);

            if (IsLogEnabled)
            {
                Context.Log.Log($"{LogPrefix}(Object[] {typeof(T)}[{count}]) {(name ?? "<no name>")}");
            }
            for (int i = 0; i < count; i++)
                // Read the value
                SerializeObject<T>(buffer[i], onPreSerialize: onPreSerialize, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer[] buffer = GetArray(obj, count);

            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}({size}[{count}]) {(name ?? "<no name>")}");

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer(buffer[i], size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer<T>[] buffer = GetArray(obj, count);

            if (IsLogEnabled)
                Context.Log.Log($"{LogPrefix}(Pointer<{typeof(T)}>[{count}]) {(name ?? "<no name>")}");

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer<T>(buffer[i], size: size, anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid, nullValue: nullValue, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

            return buffer;
        }

        public override string[] SerializeStringArray(string[] obj, long count, int length, Encoding encoding = null, string name = null)
        {
            string[] buffer = GetArray(obj, count);

            if (IsLogEnabled)
                Context.Log.Log(LogPrefix + "(String[" + count + "]) " + (name ?? "<no name>"));

            for (int i = 0; i < count; i++)
                // Read the value
                SerializeString(buffer[i], length, encoding, name: (name == null || !IsLogEnabled) ? name : $"{name}[{i}]");

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
        public void ClearWrittenObjects() {
            WrittenObjects.Clear();
        }
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
                Writer.WriteNullDelimitedString(s, Context.DefaultEncoding);

            else if (value is UInt24 u24)
                Writer.Write(u24);

            else if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                // It's nullable
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                if (underlyingType == typeof(byte))
                {
                    var v = (byte?)(object)value;
                    if (v.HasValue)
                    {
                        Writer.Write(v.Value);
                    }
                    else
                    {
                        Writer.Write((byte)0xFF);
                    }
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