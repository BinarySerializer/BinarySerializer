using System.Collections.Generic;

namespace BinarySerializer
{
    public class BGR555Color : BaseBitwiseColor
    {
        public BGR555Color() { }
        public BGR555Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public BGR555Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Blue] = new ColorChannelFormat(0, 5),
            [ColorChannel.Green] = new ColorChannelFormat(5, 5),
            [ColorChannel.Red] = new ColorChannelFormat(10, 5),
        };
    }
}