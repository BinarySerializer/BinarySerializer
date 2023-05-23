namespace BinarySerializer
{
    public class DataLengthProcessor : CalculatedValueProcessor
    {
        public DataLengthProcessor()
        {
            Flags |= BinaryProcessorFlags.Callbacks;
        }

        private Pointer _startPointer;

        public override void BeginProcessing(SerializerObject s)
        {
            _startPointer = s.CurrentPointer;
            base.BeginProcessing(s);
        }

        public override void EndProcessing(SerializerObject s)
        {
            CalculatedValue = s.CurrentPointer - _startPointer;
            base.EndProcessing(s);
        }
    }
}