using System;

namespace BinarySerializer
{
    /// <summary>
    /// A standard ARGB color
    /// </summary>
    public class CustomColor : BaseColor 
    {
        public CustomColor() { }
        public CustomColor(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }

        private float _red;
        private float _green;
        private float _blue;
        private float _alpha;

        public override float Red
        {
            get => _red;
            set
            {
                _red = value;
                OnColorModified();
            }
        }
        public override float Green
        {
            get => _green;
            set
            {
                _green = value;
                OnColorModified();
            }
        }
        public override float Blue
        {
            get => _blue;
            set
            {
                _blue = value;
                OnColorModified();
            }
        }
        public override float Alpha
        {
            get => _alpha;
            set
            {
                _alpha = value;
                OnColorModified();
            }
        }

        public override void SerializeImpl(SerializerObject s)
        {
            throw new BinarySerializableException(this, "Custom colors can't be serialized");
        }
    }
}