using System;
using System.IO;

namespace BinarySerializer
{
    public class MemoryMappedStreamFile : VirtualFile
    {
        public MemoryMappedStreamFile(
            Context context, 
            string name, 
            long baseAddress, 
            Stream stream, 
            Endian? endianness = null,
            long memoryMappedPriority = -1, 
            Pointer parentPointer = null, 
            bool leaveOpen = false) 
            : base(context, name, endianness, baseAddress, memoryMappedPriority: memoryMappedPriority, parentPointer: parentPointer)
        {
            Stream = stream ?? throw new ArgumentNullException(nameof(stream));
            Length = stream.Length;
            LeaveOpen = leaveOpen;
        }

        public MemoryMappedStreamFile(
            Context context, 
            string name, 
            long baseAddress, 
            byte[] buffer, 
            Endian? endianness = null,
            long memoryMappedPriority = -1, 
            Pointer parentPointer = null, 
            bool leaveOpen = false) 
            : this(context, name, baseAddress, new MemoryStream(buffer), endianness, memoryMappedPriority, parentPointer, leaveOpen)
        { }

        private Stream _stream;

        public override long Length { get; }
        public override bool IsMemoryMapped => true;

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