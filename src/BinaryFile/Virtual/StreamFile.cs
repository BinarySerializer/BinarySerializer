using System;
using System.IO;

namespace BinarySerializer
{
    /// <summary>
    /// A <see cref="BinaryFile"/> used for a <see cref="Stream"/>. This type of file should only be used for limited operations, such as serializing an encoded file.
    /// </summary>
    public class StreamFile : VirtualFile 
    {
        public StreamFile(Context context, string name, Stream stream, Endian? endianness = null, bool allowLocalPointers = false, Pointer parentPointer = null, bool leaveOpen = false) : base(context, name, endianness, parentPointer: parentPointer)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Length = stream.Length;
            AllowLocalPointers = allowLocalPointers;
            LeaveOpen = leaveOpen;

            // Default to current file to avoid issues with pointers being serialized within encoded data
            FileRedirectBehavior = RedirectBehavior.CurrentFile;
        }

        private Stream _stream;

        public override long Length { get; }
        public override bool IsMemoryMapped => false;

        public bool AllowLocalPointers { get; }
        public bool LeaveOpen { get; }

        protected Stream Stream
        {
            get => _stream ?? throw new ObjectDisposedException(nameof(Stream));
            set => _stream = value;
        }

        public override Reader CreateReader() 
        {
            Reader reader = new Reader(Stream, isLittleEndian: Endianness == Endian.Little, leaveOpen: LeaveOpen);
            return reader;
        }

        public override Writer CreateWriter() 
        {
            Writer writer = new Writer(Stream, isLittleEndian: Endianness == Endian.Little, leaveOpen: LeaveOpen);
            Stream.Position = 0;
            return writer;
        }

        public override BinaryFile GetPointerFile(long serializedValue, Pointer anchor = null)
        {
            if (AllowLocalPointers)
                return GetLocalPointerFile(serializedValue, anchor);
            else
                return GetMemoryMappedPointerFile(serializedValue, anchor);
        }

        public override void Dispose()
        {
            // Dispose base file
            base.Dispose();

            // Dispose and remove the reference to the stream
            if (!LeaveOpen)
                _stream?.Dispose();
            Stream = null;
        }
    }
}