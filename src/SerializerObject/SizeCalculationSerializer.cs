using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace BinarySerializer
{
    /// <summary>
    /// A serializer object used for calculating the size of structs. This does not have a source to write to and instead only saves the position of the serialization. Any <see cref="SerializerObject.DoAt"/> call is ignored to avoid serializing unrelated data.
    /// </summary>
    public class SizeCalculationSerializer : SerializerObject, IDisposable
    {
        #region Constructor

        public SizeCalculationSerializer(Context context) : base(context)
        {
            FilePositions = new Dictionary<BinaryFile, long>();
        }

        #endregion

        #region Protected Properties

        protected Dictionary<BinaryFile, long> FilePositions { get; }
        public long? CurrentFilePosition
        {
            get => FilePositions.ContainsKey(CurrentFile) ? FilePositions[CurrentFile] : (long?) null;
            set => FilePositions[CurrentFile] = value ?? 0;
        }

        protected BinaryFile CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => 0;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile;

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => CurrentFilePosition ?? 0;

        #endregion

        #region Logging

        public override void Log(string logString) { }

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

                if (encoded != null)
                    CurrentFilePosition += encoded.Length;
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
                using Stream encoded = encodedFile.Encoder.EncodeStream(encodedFile.Stream);
                encodedFile.Stream.Close();
                Context.RemoveFile(encodedFile.File);

                if (encoded != null)
                    CurrentFilePosition += encoded.Length;
            }
        }

        #endregion

        #region XOR

        public override void BeginXOR(IXORCalculator xorCalculator) { }
        public override void EndXOR() { }
        public override IXORCalculator GetXOR() => null;

        #endregion

        #region Positioning

        public override void Goto(Pointer offset)
        {
            if (offset == null)
                return;

            if (offset.File != CurrentFile)
                SwitchToFile(offset.File);

            CurrentFilePosition = offset.FileOffset;
        }

        public override void DoAt(Pointer offset, Action action) { }

        public override T DoAt<T>(Pointer offset, Func<T> action) => default;

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string name = null)
        {
            ReadType(calculatedChecksum);
            return calculatedChecksum;
        }

        /// <summary>
        /// Begins calculating byte checksum for all decrypted bytes read from the stream
        /// </summary>
        /// <param name="checksumCalculator">The checksum calculator to use</param>
        public override void BeginCalculateChecksum(IChecksumCalculator checksumCalculator) { }

        /// <summary>
        /// Ends calculating the checksum and return the value
        /// </summary>
        /// <typeparam name="T">The type of checksum value</typeparam>
        /// <returns>The checksum value</returns>
        public override T EndCalculateChecksum<T>() => default;

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string name = null)
        {
            ReadType(obj);
            return obj;
        }

        public override T SerializeObject<T>(T obj, Action<T> onPreSerialize = null, string name = null)
        {
            Depth++;

            if (obj.Offset == null)
                obj.Init(CurrentPointer);

            onPreSerialize?.Invoke(obj);
            obj.Serialize(this);
            Depth--;
            return obj;
        }

        public override Pointer SerializePointer(Pointer obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            CurrentFilePosition += size switch
            {
                PointerSize.Pointer16 => 2,
                PointerSize.Pointer32 => 4,
                PointerSize.Pointer64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

            return obj;
        }

        public override Pointer<T> SerializePointer<T>(Pointer<T> obj, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Depth++;

            CurrentFilePosition += size switch
            {
                PointerSize.Pointer16 => 2,
                PointerSize.Pointer32 => 4,
                PointerSize.Pointer64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

            if (obj != null && obj.PointerValue != null && resolve && obj.Value != null)
                DoAt(obj.PointerValue, () => SerializeObject<T>(obj.Value, onPreSerialize: onPreSerialize));

            Depth--;
            return obj;
        }

        public override string SerializeString(string obj, long? length = null, Encoding encoding = null, string name = null)
        {
            if (length.HasValue)
                CurrentFilePosition += length;
            else
                CurrentFilePosition += (encoding ?? Defaults?.StringEncoding ?? Context.DefaultEncoding).GetBytes(obj + '\0').Length;

            return obj;
        }

        #endregion

        #region Array Serialization

        public override T[] SerializeArraySize<T, U>(T[] obj, string name = null)
        {
            U Size = (U)Convert.ChangeType((obj?.Length) ?? 0, typeof(U));
            Serialize<U>(Size);
            return obj;
        }

        public override T[] SerializeArray<T>(T[] obj, long count, string name = null)
        {
            T[] buffer = GetArray(obj, count);

            if (typeof(T) == typeof(byte))
            {
                CurrentFilePosition += buffer.Length;
                return buffer;
            }

            for (int i = 0; i < count; i++)
                // Read the value
                Serialize<T>(buffer[i]);

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T[] obj, long count, Action<T> onPreSerialize = null, string name = null)
        {
            T[] buffer = GetArray(obj, count);

            for (int i = 0; i < count; i++)
                // Read the value
                SerializeObject<T>(buffer[i], onPreSerialize: onPreSerialize);

            return buffer;
        }

        public override Pointer[] SerializePointerArray(Pointer[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer[] buffer = GetArray(obj, count);

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer(buffer[i], anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue);

            return buffer;
        }

        public override Pointer<T>[] SerializePointerArray<T>(Pointer<T>[] obj, long count, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null, string name = null)
        {
            Pointer<T>[] buffer = GetArray(obj, count);

            for (int i = 0; i < count; i++)
                // Read the value
                SerializePointer<T>(buffer[i], anchor: anchor, resolve: resolve, onPreSerialize: onPreSerialize, allowInvalid: allowInvalid, nullValue: nullValue);

            return buffer;
        }

        public override string[] SerializeStringArray(string[] obj, long count, int length, Encoding encoding = null, string name = null)
        {
            for (int i = 0; i < count; i++)
                // Read the value
                SerializeString(obj[i], length, encoding);

            return obj;
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action) => action();

        public override void SerializeBitValues(Action<SerializeBits64> serializeFunc)
        {
            int totalLength = 0;

            serializeFunc((value, length, name) =>
            {
                totalLength += length;
                return value;
            });

            CurrentFilePosition += (int)Math.Ceiling(totalLength / 8f);
        }

        public override void DoBits<T>(Action<BitSerializerObject> serializeFunc) {
            // Serialize value
            Serialize<T>((T)Convert.ChangeType(0, typeof(T)));
        }
        #endregion

        #region Protected Helpers

        protected T[] GetArray<T>(T[] obj, long count)
        {
            // Create or resize array if necessary
            return obj ?? new T[(int)count];
        }

        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null)
                return;

            if (!FilePositions.ContainsKey(newFile))
                FilePositions.Add(newFile, 0);

            CurrentFile = newFile;
        }

        protected void ReadType<T>(T value)
        {
            if (value is byte[] ba)
                CurrentFilePosition += ba.Length;
            else if (value is Array a)
                foreach (var item in a)
                    ReadType(item);
            else if (value?.GetType().IsEnum == true)
                ReadType(Convert.ChangeType(value, Enum.GetUnderlyingType(value.GetType())));
            else if (value is bool)
                CurrentFilePosition += 1;
            else if (value is sbyte)
                CurrentFilePosition += 1;
            else if (value is byte)
                CurrentFilePosition += 1;
            else if (value is short)
                CurrentFilePosition += 2;
            else if (value is ushort)
                CurrentFilePosition += 2;
            else if (value is int)
                CurrentFilePosition += 4;
            else if (value is uint)
                CurrentFilePosition += 4;
            else if (value is long)
                CurrentFilePosition += 8;
            else if (value is ulong)
                CurrentFilePosition += 8;
            else if (value is float)
                CurrentFilePosition += 4;
            else if (value is double)
                CurrentFilePosition += 8;
            else if (value is string s)
                CurrentFilePosition += Context.DefaultEncoding.GetBytes(s + '\0').Length;
            else if (value is UInt24)
                CurrentFilePosition += 3;
            else if (Nullable.GetUnderlyingType(typeof(T)) != null)
            {
                // It's nullable
                var underlyingType = Nullable.GetUnderlyingType(typeof(T));
                if (underlyingType == typeof(byte))
                    CurrentFilePosition += 1;
                else
                    throw new NotSupportedException($"The specified type {typeof(T)} is not supported.");
            }
            else if (value is null)
                throw new ArgumentNullException(nameof(value));
            else
                throw new NotSupportedException($"The specified type {value.GetType().Name} is not supported.");
        }

        #endregion

        #region Disposing

        public void Dispose()
        {
            FilePositions.Clear();
        }

        public void DisposeFile(BinaryFile file)
        {
            FilePositions.Remove(file);
        }
		#endregion
	}
}