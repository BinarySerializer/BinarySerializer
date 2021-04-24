﻿using System.Collections.Generic;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color wrapper with serializing support for the encoding BGRA-5551
    /// </summary>
    public class BGRA5551Color : BaseBitwiseColor
    {
        public BGRA5551Color() { }
        public BGRA5551Color(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }
        public BGRA5551Color(uint colorValue) : base(colorValue) { }

        protected override IReadOnlyDictionary<ColorChannel, ColorChannelFormat> ColorFormatting => Format;

        protected static IReadOnlyDictionary<ColorChannel, ColorChannelFormat> Format = new Dictionary<ColorChannel, ColorChannelFormat>()
        {
            [ColorChannel.Blue] = new ColorChannelFormat(0, 5),
            [ColorChannel.Green] = new ColorChannelFormat(5, 5),
            [ColorChannel.Red] = new ColorChannelFormat(10, 5),
            [ColorChannel.Alpha] = new ColorChannelFormat(15, 1),
        };
    }
}