using System.Collections.Generic;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding RGBA-8888
    /// </summary>
    public class RGBA8888Color : BaseColor
    {
        public RGBA8888Color() { }
        public RGBA8888Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public RGBA8888Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Red] = new ColorChannelFormat(0, 8),
            [ColorChannel.Green] = new ColorChannelFormat(8, 8),
            [ColorChannel.Blue] = new ColorChannelFormat(16, 8),
            [ColorChannel.Alpha] = new ColorChannelFormat(24, 8),
        };
    }
}