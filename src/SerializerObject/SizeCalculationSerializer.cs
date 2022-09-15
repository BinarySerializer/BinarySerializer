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
        protected long? CurrentFilePosition
        {
            get
            {
                VerifyHasCurrentPointer();
                return FilePositions.TryGetValue(CurrentFile, out long v) ? v : null;
            }
            set
            {
                VerifyHasCurrentPointer();
                FilePositions[CurrentFile] = value ?? 0;
            }
        }
        protected BinaryFile? CurrentFile { get; set; }

        #endregion

        #region Public Properties

        /// <summary>
        /// The current length of the data being serialized
        /// </summary>
        public override long CurrentLength => 0;

        [MemberNotNullWhen(true, nameof(CurrentFile))]
        public override bool HasCurrentPointer => CurrentFile != null;

        /// <summary>
        /// The current binary file being used by the serializer
        /// </summary>
        public override BinaryFile CurrentBinaryFile => CurrentFile ?? throw new SerializerMissingCurrentPointerException();

        /// <summary>
        /// The current file offset
        /// </summary>
        public override long CurrentFileOffset => CurrentFilePosition ?? 0;

        #endregion

        #region Logging

        public override void Log(string logString, params object?[] args) { }

        #endregion

        #region Encoding

        public override void DoEncoded(
            IStreamEncoder? encoder,
            Action action,
            Endian? endianness = null,
            bool allowLocalPointers = false,
            string? filename = null)
        {
            if (encoder == null)
            {
                action();
                return;
            }

            VerifyHasCurrentPointer();

            // Encode the data into a stream
            using MemoryStream encoded = new();
            using MemoryStream memStream = new();

            // Stream key
            string key = filename ?? $"{CurrentPointer}_{encoder.Name}";

            // Add the stream
            StreamFile sf = new(
                context: Context,
                name: key,
                stream: memStream,
                endianness: endianness ?? CurrentFile.Endianness,
                allowLocalPointers: allowLocalPointers,
                parentPointer: CurrentPointer);

            try
            {
                Context.AddFile(sf);

                Pointer currentOffset = CurrentPointer;
                Goto(sf.StartPointer);

                try
                {
                    action();
                    memStream.Position = 0;
                    encoder.EncodeStream(memStream, encoded);
                }
                finally
                {
                    Goto(currentOffset);
                }
            }
            finally
            {
                Context.RemoveFile(sf);
            }

            CurrentFilePosition += encoded.Length;
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
            EncodedState? encodedFile = EncodedFiles.FirstOrDefault(ef => ef.File == endPointer.File);
            
            if (encodedFile == null) 
                return;

            VerifyHasCurrentPointer();

            EncodedFiles.Remove(encodedFile);

            encodedFile.Stream.Position = 0;
            using MemoryStream encoded = new();
            encodedFile.Encoder.EncodeStream(encodedFile.Stream, encoded);
            encodedFile.Stream.Close();
            Context.RemoveFile(encodedFile.File);
            CurrentFilePosition += encoded.Length;
        }

        #endregion

        #region Positioning

        public override void Goto(Pointer? offset)
        {
            if (offset == null)
                return;

            if (offset.File != CurrentFile || !HasCurrentPointer)
                SwitchToFile(offset.File);

            CurrentFilePosition = offset.FileOffset;
        }

        public override void DoAt(Pointer? offset, Action action) { }

        // TODO: Resolve issues with this when not used
        public override T DoAt<T>(Pointer? offset, Func<T> action) => default;

        #endregion

        #region Checksum

        public override T SerializeChecksum<T>(T calculatedChecksum, string? name = null)
        {
            ReadType(typeof(T));
            return calculatedChecksum;
        }

        #endregion

        #region Serialization

        public override T Serialize<T>(T obj, string? name = null)
        {
            ReadType(typeof(T));
            return obj;
        }

        public override T? SerializeNullable<T>(T? obj, string? name = null)
        {
            ReadType(typeof(T));
            return obj;
        }

        public override T SerializeObject<T>(T? obj, Action<T>? onPreSerialize = null, string? name = null)
            where T : class
        {
            obj ??= new T();

            try
            {
                Depth++;

                if (obj.Offset == null)
                    obj.Init(CurrentPointer);

                onPreSerialize?.Invoke(obj);
                obj.Serialize(this);
            }
            finally
            {
                Depth--;
            }
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
            CurrentFilePosition += size switch
            {
                PointerSize.Pointer16 => 2,
                PointerSize.Pointer32 => 4,
                PointerSize.Pointer64 => 8,
                _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
            };

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
            try
            {
                Depth++;

                CurrentFilePosition += size switch
                {
                    PointerSize.Pointer16 => 2,
                    PointerSize.Pointer32 => 4,
                    PointerSize.Pointer64 => 8,
                    _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
                };
            }
            finally
            {
                Depth--;
            }
            return obj ?? new Pointer<T>();
        }

        public override string SerializeString(string? obj, long? length = null, Encoding? encoding = null, string? name = null)
        {
            obj ??= String.Empty;

            if (length.HasValue)
                CurrentFilePosition += length;
            else
                CurrentFilePosition += (encoding ?? Defaults?.StringEncoding ?? Context.DefaultEncoding).GetBytes(obj + '\0').Length;

            return obj;
        }

        #endregion

        #region Array Serialization

        public override T?[] SerializeArraySize<T, U>(T?[]? obj, string? name = null)
            where T : default
        {
            obj ??= Array.Empty<T?>();
            U Size = (U)Convert.ChangeType(obj, typeof(U));
            Serialize<U>(Size);
            return obj;
        }

        public override T[] SerializeArray<T>(T[]? obj, long count, string? name = null)
        {
            T[] buffer = obj ?? new T[count];

            if (typeof(T) == typeof(byte))
            {
                CurrentFilePosition += buffer.Length;
                return buffer;
            }

            for (int i = 0; i < count; i++)
                buffer[i] = Serialize<T>(buffer[i]);

            return buffer;
        }

        public override T?[] SerializeNullableArray<T>(T?[]? obj, long count, string? name = null)
        {
            T?[] buffer = obj ?? new T?[count];

            for (int i = 0; i < count; i++)
                buffer[i] = SerializeNullable<T>(buffer[i]);

            return buffer;
        }

        public override T[] SerializeObjectArray<T>(T?[]? obj, long count, Action<T, int>? onPreSerialize = null, string? name = null)
            where T : class
        {
            T?[] buffer = obj ?? new T?[count];

            for (int i = 0; i < count; i++)
                // ReSharper disable once AccessToModifiedClosure
                buffer[i] = SerializeObject<T>(buffer[i], onPreSerialize: onPreSerialize == null ? (Action<T>?)null : x => onPreSerialize(x, i));

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

            for (int i = 0; i < count; i++)
                buffer[i] = SerializePointer(buffer[i], anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue);

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

            for (int i = 0; i < count; i++)
                buffer[i] = SerializePointer<T>(
                    obj: buffer[i],
                    anchor: anchor,
                    allowInvalid: allowInvalid,
                    nullValue: nullValue);

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

            for (int i = 0; i < count; i++)
                buffer[i] = SerializeString(buffer[i], length, encoding);

            return buffer!;
        }

        public override T[] SerializeArrayUntil<T>(
            T[]? obj,
            Func<T, bool> conditionCheckFunc,
            Func<T>? getLastObjFunc = null,
            string? name = null)
        {
            obj ??= Array.Empty<T>();
            T[] array = obj;

            if (getLastObjFunc != null)
                array = array.Append(getLastObjFunc()).ToArray();

            SerializeArray<T>(array, array.Length, name: name);

            return obj;
        }

        public override T?[] SerializeNullableArrayUntil<T>(
            T?[]? obj, 
            Func<T?, bool> conditionCheckFunc, 
            Func<T?>? getLastObjFunc = null,
            string? name = null)
        {
            obj ??= Array.Empty<T?>();
            T?[] array = obj;

            if (getLastObjFunc != null)
                array = array.Append(getLastObjFunc()).ToArray();

            SerializeNullableArray<T>(array, array.Length, name: name);

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

            T?[] array = obj;

            if (getLastObjFunc != null)
                array = array.Append(getLastObjFunc()).ToArray();

            SerializeObjectArray<T>(array, array.Length, onPreSerialize: onPreSerialize, name: name);

            return obj!;
        }

        #endregion

        #region Other Serialization

        public override void DoEndian(Endian endianness, Action action)
        {
            if (action == null) 
                throw new ArgumentNullException(nameof(action));
            
            action();
        }

        public override void SerializeBitValues(Action<SerializeBits64> serializeFunc)
        {
            if (serializeFunc == null) 
                throw new ArgumentNullException(nameof(serializeFunc));
            
            int totalLength = 0;

            serializeFunc((value, length, _) =>
            {
                totalLength += length;
                return value;
            });

            CurrentFilePosition += (int)Math.Ceiling(totalLength / 8f);
        }

        public override void DoBits<T>(Action<BitSerializerObject> serializeFunc) 
        {
            // Serialize value
            Serialize<T>((T)Convert.ChangeType(0, typeof(T)));
        }

        #endregion

        #region Protected Helpers

        [MemberNotNull(nameof(CurrentFile))]
        protected void VerifyHasCurrentPointer()
        {
            if (!HasCurrentPointer)
                throw new SerializerMissingCurrentPointerException();
        }

        [MemberNotNull(nameof(CurrentFile))]
        protected void SwitchToFile(BinaryFile newFile)
        {
            if (newFile == null)
                throw new ArgumentNullException(nameof(newFile));

            if (!FilePositions.ContainsKey(newFile))
                FilePositions.Add(newFile, 0);

            CurrentFile = newFile;
        }

        protected void ReadType(Type type)
        {
            if (type == null) 
                throw new ArgumentNullException(nameof(type));

            TypeCode typeCode = Type.GetTypeCode(type);

            CurrentFilePosition += typeCode switch
            {
                TypeCode.Boolean => 1,

                TypeCode.SByte => 1,
                TypeCode.Byte => 1,

                TypeCode.Int16 => 2,
                TypeCode.UInt16 => 2,

                TypeCode.Int32 => 4,
                TypeCode.UInt32 => 4,

                TypeCode.Int64 => 8,
                TypeCode.UInt64 => 8,

                TypeCode.Single => 4,
                TypeCode.Double => 8,

                TypeCode.Object when type == typeof(UInt24) => 3,

                _ => throw new NotSupportedException($"The specified type ('{type.Name}') is not supported")
            };
        }

        #endregion

        #region Disposing

        public void Dispose()
        {
            FilePositions.Clear();
        }

        public void DisposeFile(BinaryFile? file)
        {
            if (file == null)
                return;

            FilePositions.Remove(file);
        }

        #endregion
    }
}