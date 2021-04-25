namespace BinarySerializer
{
    public abstract class BaseBytewiseRGBAColor : BaseBytewiseRGBColor
    {
        protected BaseBytewiseRGBAColor() { }
        protected BaseBytewiseRGBAColor(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }

        private byte _a;

        public byte A
        {
            get => _a;
            set
            {
                _a = value;
                OnColorModified();
            }
        }
    }
}