using System.Collections.Generic;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding BGR-888
    /// </summary>
    public class BGR888Color : BaseColor
    {
        public BGR888Color() { }
        public BGR888Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public BGR888Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Blue] = new ColorChannelFormat(0, 8),
            [ColorChannel.Green] = new ColorChannelFormat(8, 8),
            [ColorChannel.Red] = new ColorChannelFormat(16, 8),
        };
    }
}