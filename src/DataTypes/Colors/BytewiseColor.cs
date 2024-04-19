namespace BinarySerializer
{
    public static class BytewiseColor
    {
        public static SerializableColor RGB666(SerializerObject s, SerializableColor obj)
        {
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 6), name: nameof(SerializableColor.Red));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 6), name: nameof(SerializableColor.Green));
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 6), name: nameof(SerializableColor.Blue));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 6),
                green: SerializableColorHelpers.ToFloat(green, 6),
                blue: SerializableColorHelpers.ToFloat(blue, 6));
        }

        public static SerializableColor RGB777(SerializerObject s, SerializableColor obj)
        {
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 7), name: nameof(SerializableColor.Red));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 7), name: nameof(SerializableColor.Green));
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 7), name: nameof(SerializableColor.Blue));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 7),
                green: SerializableColorHelpers.ToFloat(green, 7),
                blue: SerializableColorHelpers.ToFloat(blue, 7));
        }

        public static SerializableColor RGB888(SerializerObject s, SerializableColor obj)
        {
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 8), name: nameof(SerializableColor.Red));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 8), name: nameof(SerializableColor.Green));
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 8), name: nameof(SerializableColor.Blue));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 8),
                green: SerializableColorHelpers.ToFloat(green, 8),
                blue: SerializableColorHelpers.ToFloat(blue, 8));
        }

        public static SerializableColor RGBA8888(SerializerObject s, SerializableColor obj)
        {
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 8), name: nameof(SerializableColor.Red));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 8), name: nameof(SerializableColor.Green));
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 8), name: nameof(SerializableColor.Blue));
            byte alpha = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Alpha, 8), name: nameof(SerializableColor.Alpha));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 8),
                green: SerializableColorHelpers.ToFloat(green, 8),
                blue: SerializableColorHelpers.ToFloat(blue, 8),
                alpha: SerializableColorHelpers.ToFloat(alpha, 8));
        }

        public static SerializableColor BGR888(SerializerObject s, SerializableColor obj)
        {
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 8), name: nameof(SerializableColor.Blue));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 8), name: nameof(SerializableColor.Green));
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 8), name: nameof(SerializableColor.Red));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 8),
                green: SerializableColorHelpers.ToFloat(green, 8),
                blue: SerializableColorHelpers.ToFloat(blue, 8));
        }

        public static SerializableColor BGRA8888(SerializerObject s, SerializableColor obj)
        {
            byte blue = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Blue, 8), name: nameof(SerializableColor.Blue));
            byte green = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Green, 8), name: nameof(SerializableColor.Green));
            byte red = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Red, 8), name: nameof(SerializableColor.Red));
            byte alpha = s.Serialize<byte>(SerializableColorHelpers.ToByte(obj.Alpha, 8), name: nameof(SerializableColor.Alpha));

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 8),
                green: SerializableColorHelpers.ToFloat(green, 8),
                blue: SerializableColorHelpers.ToFloat(blue, 8),
                alpha: SerializableColorHelpers.ToFloat(alpha, 8));
        }
    }
}