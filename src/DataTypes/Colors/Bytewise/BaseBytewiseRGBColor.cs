namespace BinarySerializer
{
    public abstract class BaseBytewiseRGBColor : BaseColor
    {
        protected BaseBytewiseRGBColor() { }
        protected BaseBytewiseRGBColor(float r, float g, float b, float a = 1f) : base(r, g, b, a) { }

        private byte _r;
        private byte _g;
        private byte _b;

        public byte R
        {
            get => _r;
            set
            {
                _r = value;
                OnColorModified();
            }
        }
        public byte G
        {
            get => _g;
            set
            {
                _g = value;
                OnColorModified();
            }
        }
        public byte B
        {
            get => _b;
            set
            {
                _b = value;
                OnColorModified();
            }
        }
    }
}