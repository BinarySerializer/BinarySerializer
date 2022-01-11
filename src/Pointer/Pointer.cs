using System;

namespace BinarySerializer
{
    public class Pointer : IEquatable<Pointer>, IComparable<Pointer> 
    {
        #region Constructor

        public Pointer(long offset, BinaryFile file, Pointer anchor = null, PointerSize size = PointerSize.Pointer32, OffsetType offsetType = OffsetType.Serialized)
        {
            Anchor = anchor;
            File = file;
            Size = size;
            Context = file.Context;

            AbsoluteOffset = offsetType switch
            {
                OffsetType.Serialized when anchor != null => anchor.AbsoluteOffset + offset,
                OffsetType.File => (offset + File?.BaseAddress) ?? offset,
                _ => offset
            };

            if (offsetType == OffsetType.File)
                FileOffset = offset;
            else
                FileOffset = (AbsoluteOffset - File?.BaseAddress) ?? AbsoluteOffset;

            if (Context != null && Context.SavePointersForRelocation && file.SavePointersToMemoryMap)
                Context.MemoryMap.AddPointer(this);
        }

        #endregion

        #region Public Properties

        public Context Context { get; }
        public Pointer Anchor { get; private set; }
        public BinaryFile File { get; }
        public PointerSize Size { get; }

        public long AbsoluteOffset { get; }
        public long FileOffset { get; }

        public long SerializedOffset
        {
            get
            {
                var off = AbsoluteOffset;
                if (Anchor != null)
                    off -= Anchor.AbsoluteOffset;
                return off;
            }
        }

        public string StringFileOffset => GetAddressString(FileOffset);
        public string StringAbsoluteOffset => GetAddressString(AbsoluteOffset);

        protected virtual string GetAddressString(long value)
        {
            return Size switch
            {
                PointerSize.Pointer16 => $"{value:X4}",
                PointerSize.Pointer32 => $"{value:X8}",
                PointerSize.Pointer64 => $"{value:X16}",
                _ => $"{value:X8}"
            };
        }

        #endregion

        #region Public Methods

        public override string ToString()
        {
            long regionOffset = 0;

            // Attempt to get a region
            BinaryFile.Region region = File?.GetRegion(FileOffset);

            // Get the offset within the region
            if (region != null)
                regionOffset = FileOffset - region.Offset;

            // Attempt to get a label
            string label = File?.GetLabel(FileOffset);

            // Create the initial string
            var str = $"{File?.FilePath ?? "<no file>"}|0x{GetAddressString(AbsoluteOffset)}";

            // Add file offset if the file is memory mapped
            if (File != null && File.BaseAddress != 0) 
                str += $"[0x{GetAddressString(FileOffset)}]";

            // Add serialized offset if an anchor is specified
            if (Anchor != null)
                str += $"<0x{GetAddressString(SerializedOffset)}>";

            // Append region info
            if (region != null) 
                str += $"({region.Name}:0x{GetAddressString(regionOffset)})";

            // Append label
            if (label != null)
                str += $"({label})";

            return str;
        }

        #endregion

        #region Equality and comparison

        public override bool Equals(object obj) => obj is Pointer p && this == p;
        public override int GetHashCode() => AbsoluteOffset.GetHashCode() ^ File.GetHashCode();
        public bool Equals(Pointer other) => this == other;
        public int CompareTo(Pointer other)
        {
            if (ReferenceEquals(this, other))
                return 0;

            if (other is null)
                return 1;

            return AbsoluteOffset.CompareTo(other.AbsoluteOffset);
        }

        #endregion

        #region Operators

        public static bool operator ==(Pointer x, Pointer y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return x.AbsoluteOffset == y.AbsoluteOffset && x.File == y.File;
        }
        public static bool operator !=(Pointer x, Pointer y) => !(x == y);

        public static Pointer operator +(Pointer x, long y) => new Pointer(x.AbsoluteOffset + y, x.File, size: x.Size) { Anchor = x.Anchor };
        public static Pointer operator -(Pointer x, long y) => new Pointer(x.AbsoluteOffset - y, x.File, size: x.Size) { Anchor = x.Anchor };

        public static long operator +(Pointer x, Pointer y) => x.AbsoluteOffset + y.AbsoluteOffset;
        public static long operator -(Pointer x, Pointer y) => x.AbsoluteOffset - y.AbsoluteOffset;

        #endregion

        #region Data Types

        public enum OffsetType
        {
            Serialized,
            Absolute,
            File,
        }

        #endregion
    }

    public class Pointer<T> 
        where T : BinarySerializable, new() 
    {
        public Pointer(Pointer pointerValue, bool resolve = false, SerializerObject s = null, Action<T> onPreSerialize = null) 
        {
            Context = pointerValue?.Context;
            PointerValue = pointerValue;

            if (resolve)
                Resolve(s, onPreSerialize: onPreSerialize);
        }

        public Pointer(SerializerObject s, PointerSize size = PointerSize.Pointer32, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false, long? nullValue = null) 
        {
            PointerValue = s.SerializePointer(PointerValue, size: size, anchor: anchor, allowInvalid: allowInvalid, nullValue: nullValue, name: "Pointer");

            if (resolve)
                Resolve(s, onPreSerialize: onPreSerialize);
        }
        public Pointer(Pointer pointerValue, T value)
        {
            PointerValue = pointerValue;
            Value = value;
        }
        public Pointer()
        {
            PointerValue = null;
            Value = null;
        }

        public Context Context { get; }
        public Pointer PointerValue { get; }
        public T Value { get; set; }

        public Pointer<T> Resolve(SerializerObject s, Action<T> onPreSerialize = null)
        {
            if (s == null) 
                throw new ArgumentNullException(nameof(s));
            
            if (PointerValue == null) 
                return this;
            
            Value = PointerValue.Context.Cache.FromOffset<T>(PointerValue);
            s.DoAt(PointerValue, () => Value = s.SerializeObject<T>(Value, onPreSerialize: onPreSerialize, name: nameof(Value)));
            
            return this;
        }
        public Pointer<T> Resolve(Context c) 
        {
            if (c == null) 
                throw new ArgumentNullException(nameof(c));
            
            if (PointerValue != null)
                Value = c.Cache.FromOffset<T>(PointerValue);

            return this;
        }

        public static implicit operator T(Pointer<T> a) => a.Value;
        public static implicit operator Pointer<T>(T t) => t == null ? new Pointer<T>(null, null) : new Pointer<T>(t.Offset, t);
    }
}