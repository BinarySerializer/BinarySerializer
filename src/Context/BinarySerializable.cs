namespace BinarySerializer
{
    /// <summary>
    /// Base type for serializable structs
    /// </summary>
    public abstract class BinarySerializable 
    {
        /// <summary>
        /// Indicates if it's the first time the struct is loaded
        /// </summary>
		protected bool IsFirstLoad { get; set; } = true;

        /// <summary>
        /// The context
        /// </summary>
		public Context Context { get; protected set; }
		
        /// <summary>
        /// The struct offset
        /// </summary>
        public Pointer Offset { get; protected set; }

        /// <summary>
        /// The struct size
        /// </summary>
        public virtual long Size { get; protected set; }

        /// <summary>
        /// Indicates whether this object should be logged on one line
        /// </summary>
        public virtual bool IsShortLog { get; } = false;

        /// <summary>
        /// The string for displaying this object on one line
        /// </summary>
        public virtual string ShortLog => null;

        /// <summary>
        /// Initializes the struct from an offset
        /// </summary>
        /// <param name="offset">The offset the struct is located at</param>
        public void Init(Pointer offset) 
        {
			Offset = offset;
            if (Context != null && offset.Context != Context) 
                OnChangeContext(Context, offset.Context);
            Context = offset.Context;
        }

        /// <summary>
        /// Handles the data serialization
        /// </summary>
        /// <param name="s">The serializer object</param>
        public abstract void SerializeImpl(SerializerObject s);

        /// <summary>
        /// Serializes the data struct
        /// </summary>
        /// <param name="s">The serializer</param>
		public void Serialize(SerializerObject s) 
        {
			OnPreSerialize(s);
			SerializeImpl(s);
			Size = s.CurrentAbsoluteOffset - Offset.AbsoluteOffset;
			OnPostSerialize(s);
			IsFirstLoad = false;
		}

		protected virtual void OnPreSerialize(SerializerObject s) { }
		protected virtual void OnPostSerialize(SerializerObject s) { }

		/// <summary>
		/// Recalculates the <see cref="Size"/> value of the object
		/// </summary>
		public virtual void RecalculateSize()
        {
            // Create a serialize for calculating the size
            using var s = new SizeCalculationSerializer(Context);
            
            // Go to the offset of the object
            s.Goto(Offset);

            // Serialize the object
            Serialize(s);
        }

        protected virtual void OnChangeContext(Context oldContext, Context newContext) {
        }
    }
}