using System.Collections.Generic;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding RGB-888
    /// </summary>
    public class RGB888Color : BaseColor
    {
        public RGB888Color() { }
        public RGB888Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public RGB888Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Red] = new ColorChannelFormat(0, 8),
            [ColorChannel.Green] = new ColorChannelFormat(8, 8),
            [ColorChannel.Blue] = new ColorChannelFormat(16, 8),
        };
    }
}