using System.Collections.Generic;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding RGB-666
    /// </summary>
    public class RGB666Color : BaseColor
    {
        public RGB666Color() { }
        public RGB666Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public RGB666Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Red] = new ColorChannelFormat(0, 6),
            [ColorChannel.Green] = new ColorChannelFormat(8, 6),
            [ColorChannel.Blue] = new ColorChannelFormat(16, 6),
        };
    }
}