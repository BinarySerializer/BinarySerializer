using System;

namespace BinarySerializer
{
    public readonly struct SerializableColor : IEquatable<SerializableColor>, ISerializerShortLog
    {
        #region Constructors

        public SerializableColor(float red, float green, float blue)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = 1;
        }

        public SerializableColor(float red, float green, float blue, float alpha)
        {
            Red = red;
            Green = green;
            Blue = blue;
            Alpha = alpha;
        }

        #endregion

        #region Public Static Properties

        public static SerializableColor Clear { get; } = new(0, 0, 0, 0);
        public static SerializableColor Black { get; } = new(0, 0, 0, 1);
        public static SerializableColor White { get; } = new(1, 1, 1, 1);

        #endregion

        #region Public Properties

        public float Red { get; }
        public float Green { get; }
        public float Blue { get; }
        public float Alpha { get; }

        string ISerializerShortLog.ShortLog => ToString();

        #endregion

        #region Operators

        public static bool operator ==(SerializableColor x, SerializableColor y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(SerializableColor x, SerializableColor y) => !(x == y);

        #endregion

        #region Public Methods

        public bool Equals(SerializableColor other)
        {
            return Alpha == other.Alpha && Red == other.Red && Green == other.Green && Blue == other.Blue;
        }

        public override bool Equals(object obj)
        {
            return obj is SerializableColor c && Equals(c);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Alpha.GetHashCode();
                hashCode = (hashCode * 397) ^ Red.GetHashCode();
                hashCode = (hashCode * 397) ^ Green.GetHashCode();
                hashCode = (hashCode * 397) ^ Blue.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString() => $"RGBA({(int)(Red * 255)}, {(int)(Green * 255)}, {(int)(Blue * 255)}, {Alpha})";

        #endregion
    }
}