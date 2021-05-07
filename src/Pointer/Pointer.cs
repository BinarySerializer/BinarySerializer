using System;

namespace BinarySerializer
{
    public class Pointer : IEquatable<Pointer>, IComparable<Pointer> 
    {
        #region Constructor

        public Pointer(long offset, BinaryFile file, Pointer anchor = null)
        {
            if (anchor != null)
            {
                Anchor = anchor;
                offset = anchor.AbsoluteOffset + offset;
            }

            AbsoluteOffset = offset;
            File = file;
            Context = file.Context;
            FileOffset = AbsoluteOffset - File?.BaseAddress ?? AbsoluteOffset;

            if (Context != null && Context.SavePointersForRelocation && file.SavePointersToMemoryMap)
                Context.MemoryMap.AddPointer(this);
        }

        #endregion

        #region Public Properties

        public Context Context { get; }
        public Pointer Anchor { get; private set; }
        public BinaryFile File { get; }

        public long AbsoluteOffset { get; }
        public long FileOffset { get; }

        public uint SerializedOffset
        {
            get
            {
                uint off = (uint)AbsoluteOffset;
                if (Anchor != null)
                    off -= (uint)Anchor.AbsoluteOffset;
                return off;
            }
        }

        public string StringFileOffset => GetAddressString(FileOffset);
        public string StringAbsoluteOffset => GetAddressString(AbsoluteOffset);

        protected virtual string GetAddressString(long value) => $"{value:X8}";

        #endregion

        #region Public Methods

        public Pointer SetAnchor(Pointer anchor)
        {
            Pointer ptr = new Pointer(AbsoluteOffset, File)
            {
                Anchor = anchor
            };
            return ptr;
        }

        public override string ToString()
        {
            BinaryFile.Region region = null;
            var fileOffset = FileOffset;
            long regionOffset = 0;
            if (File != null)
            {
                region = File.GetRegion(fileOffset);
                if (region != null) regionOffset = fileOffset - region.Offset;
            }
            var str = $"{File.FilePath}|0x{GetAddressString(AbsoluteOffset)}";
            if (File != null && File.BaseAddress != 0) str += $"[0x{GetAddressString(fileOffset)}]";
            if (region != null) str += $"({region.Name}:0x{GetAddressString(regionOffset)})";
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

        public static Pointer operator +(Pointer x, long y) => new Pointer(x.AbsoluteOffset + y, x.File) { Anchor = x.Anchor };
        public static Pointer operator -(Pointer x, long y) => new Pointer(x.AbsoluteOffset - y, x.File) { Anchor = x.Anchor };

        public static long operator +(Pointer x, Pointer y) => x.AbsoluteOffset + y.AbsoluteOffset;
        public static long operator -(Pointer x, Pointer y) => x.AbsoluteOffset - y.AbsoluteOffset;

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

        public Pointer(SerializerObject s, Pointer anchor = null, bool resolve = false, Action<T> onPreSerialize = null, bool allowInvalid = false) 
        {
            PointerValue = s.SerializePointer(PointerValue, anchor: anchor, allowInvalid: allowInvalid, name: "Pointer");
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
            if (PointerValue != null) {
                Value = PointerValue.Context.Cache.FromOffset<T>(PointerValue);
                s.DoAt(PointerValue, () => {
                    Value = s.SerializeObject<T>(Value, onPreSerialize: onPreSerialize, name: "Value");
                });
            }
            return this;
        }
        public Pointer<T> Resolve(Context c) 
        {
            if (PointerValue != null)
                Value = c.Cache.FromOffset<T>(PointerValue);

            return this;
        }

        public static implicit operator T(Pointer<T> a) => a.Value;
        public static implicit operator Pointer<T>(T t) => t == null ? new Pointer<T>(null, null) : new Pointer<T>(t.Offset, t);
    }
}