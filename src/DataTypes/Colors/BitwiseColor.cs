namespace BinarySerializer
{
    public static class BitwiseColor
    {
        public static SerializableColor ABGR1555(SerializerObject s, SerializableColor obj)
        {
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 1);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 5);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);

            s.DoBits<ushort>(b =>
            {
                alpha = b.SerializeBits<byte>(alpha, 1, name: nameof(SerializableColor.Alpha));
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                green = b.SerializeBits<byte>(green, 5, name: nameof(SerializableColor.Green));
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 5),
                blue: SerializableColorHelpers.ToFloat(blue, 5),
                alpha: SerializableColorHelpers.ToFloat(alpha, 1));
        }

        public static SerializableColor BGR565(SerializerObject s, SerializableColor obj)
        {
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 6);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);

            s.DoBits<ushort>(b =>
            {
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                green = b.SerializeBits<byte>(green, 6, name: nameof(SerializableColor.Green));
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 6),
                blue: SerializableColorHelpers.ToFloat(blue, 5));
        }

        public static SerializableColor BGRA4441(SerializerObject s, SerializableColor obj)
        {
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 4);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 4);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 4);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 1);

            s.DoBits<ushort>(b =>
            {
                blue = b.SerializeBits<byte>(blue, 4, name: nameof(SerializableColor.Blue));
                green = b.SerializeBits<byte>(green, 4, name: nameof(SerializableColor.Green));
                red = b.SerializeBits<byte>(red, 4, name: nameof(SerializableColor.Red));
                b.SerializePadding(3);
                alpha = b.SerializeBits<byte>(alpha, 1, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 4),
                green: SerializableColorHelpers.ToFloat(green, 4),
                blue: SerializableColorHelpers.ToFloat(blue, 4),
                alpha: SerializableColorHelpers.ToFloat(alpha, 1));
        }

        public static SerializableColor BGRA4444(SerializerObject s, SerializableColor obj)
        {
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 4);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 4);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 4);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 4);

            s.DoBits<ushort>(b =>
            {
                blue = b.SerializeBits<byte>(blue, 4, name: nameof(SerializableColor.Blue));
                green = b.SerializeBits<byte>(green, 4, name: nameof(SerializableColor.Green));
                red = b.SerializeBits<byte>(red, 4, name: nameof(SerializableColor.Red));
                alpha = b.SerializeBits<byte>(alpha, 4, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 4),
                green: SerializableColorHelpers.ToFloat(green, 4),
                blue: SerializableColorHelpers.ToFloat(blue, 4),
                alpha: SerializableColorHelpers.ToFloat(alpha, 4));
        }

        public static SerializableColor BGRA5551(SerializerObject s, SerializableColor obj)
        {
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 5);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 1);

            s.DoBits<ushort>(b =>
            {
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                green = b.SerializeBits<byte>(green, 5, name: nameof(SerializableColor.Green));
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
                alpha = b.SerializeBits<byte>(alpha, 1, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 5),
                blue: SerializableColorHelpers.ToFloat(blue, 5),
                alpha: SerializableColorHelpers.ToFloat(alpha, 1));
        }

        public static SerializableColor GBR655(SerializerObject s, SerializableColor obj)
        {
            byte green = SerializableColorHelpers.ToByte(obj.Green, 6);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);

            s.DoBits<ushort>(b =>
            {
                green = b.SerializeBits<byte>(green, 6, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 6),
                blue: SerializableColorHelpers.ToFloat(blue, 5));
        }

        public static SerializableColor RGB555(SerializerObject s, SerializableColor obj)
        {
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 5);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);

            s.DoBits<ushort>(b =>
            {
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
                green = b.SerializeBits<byte>(green, 5, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                // 1 unused bit
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 5),
                blue: SerializableColorHelpers.ToFloat(blue, 5));
        }

        public static SerializableColor RGB565(SerializerObject s, SerializableColor obj)
        {
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 6);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);

            s.DoBits<ushort>(b =>
            {
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
                green = b.SerializeBits<byte>(green, 6, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 6),
                blue: SerializableColorHelpers.ToFloat(blue, 5));
        }

        public static SerializableColor RGBA4444(SerializerObject s, SerializableColor obj)
        {
            byte red = SerializableColorHelpers.ToByte(obj.Red, 4);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 4);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 4);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 4);

            s.DoBits<ushort>(b =>
            {
                red = b.SerializeBits<byte>(red, 4, name: nameof(SerializableColor.Red));
                green = b.SerializeBits<byte>(green, 4, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 4, name: nameof(SerializableColor.Blue));
                alpha = b.SerializeBits<byte>(alpha, 4, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 4),
                green: SerializableColorHelpers.ToFloat(green, 4),
                blue: SerializableColorHelpers.ToFloat(blue, 4),
                alpha: SerializableColorHelpers.ToFloat(alpha, 4));
        }

        public static SerializableColor RGBA5551(SerializerObject s, SerializableColor obj)
        {
            byte red = SerializableColorHelpers.ToByte(obj.Red, 5);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 5);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 5);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 1);

            s.DoBits<ushort>(b =>
            {
                red = b.SerializeBits<byte>(red, 5, name: nameof(SerializableColor.Red));
                green = b.SerializeBits<byte>(green, 5, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 5, name: nameof(SerializableColor.Blue));
                alpha = b.SerializeBits<byte>(alpha, 1, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 5),
                green: SerializableColorHelpers.ToFloat(green, 5),
                blue: SerializableColorHelpers.ToFloat(blue, 5),
                alpha: SerializableColorHelpers.ToFloat(alpha, 1));
        }

        public static SerializableColor RGBA8888(SerializerObject s, SerializableColor obj)
        {
            byte red = SerializableColorHelpers.ToByte(obj.Red, 8);
            byte green = SerializableColorHelpers.ToByte(obj.Green, 8);
            byte blue = SerializableColorHelpers.ToByte(obj.Blue, 8);
            byte alpha = SerializableColorHelpers.ToByte(obj.Alpha, 8);

            s.DoBits<uint>(b =>
            {
                red = b.SerializeBits<byte>(red, 8, name: nameof(SerializableColor.Red));
                green = b.SerializeBits<byte>(green, 8, name: nameof(SerializableColor.Green));
                blue = b.SerializeBits<byte>(blue, 8, name: nameof(SerializableColor.Blue));
                alpha = b.SerializeBits<byte>(alpha, 8, name: nameof(SerializableColor.Alpha));
            });

            return new SerializableColor(
                red: SerializableColorHelpers.ToFloat(red, 8),
                green: SerializableColorHelpers.ToFloat(green, 8),
                blue: SerializableColorHelpers.ToFloat(blue, 8),
                alpha: SerializableColorHelpers.ToFloat(alpha, 8));
        }
    }
}