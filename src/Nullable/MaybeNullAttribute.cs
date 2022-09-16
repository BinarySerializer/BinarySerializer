#nullable enable
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    ///     Specifies that an output may be <see langword="null"/> even if the
    ///     corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.Property | AttributeTargets.ReturnValue, 
        Inherited = false)]
    [ExcludeFromCodeCoverage, DebuggerNonUserCode]
    internal sealed class MaybeNullAttribute : Attribute
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MaybeNullAttribute"/> class.
        /// </summary>
        public MaybeNullAttribute() { }
    }
}