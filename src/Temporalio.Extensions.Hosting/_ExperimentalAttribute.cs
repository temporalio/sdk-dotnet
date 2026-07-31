#pragma warning disable SA1649

#if !NET8_0_OR_GREATER
namespace System.Diagnostics.CodeAnalysis
{
    /// <summary>
    /// Needed for marking APIs as experimental on older .NET versions. The compiler matches this
    /// attribute by full name, so it behaves the same as the .NET 8+ built-in one.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Assembly |
        AttributeTargets.Module |
        AttributeTargets.Class |
        AttributeTargets.Struct |
        AttributeTargets.Enum |
        AttributeTargets.Constructor |
        AttributeTargets.Method |
        AttributeTargets.Property |
        AttributeTargets.Field |
        AttributeTargets.Event |
        AttributeTargets.Interface |
        AttributeTargets.Delegate,
        Inherited = false)]
    internal sealed class ExperimentalAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ExperimentalAttribute"/> class.
        /// </summary>
        /// <param name="diagnosticId">Identifier of the diagnostic reported for uses of the
        /// experimental API.</param>
        public ExperimentalAttribute(string diagnosticId) => DiagnosticId = diagnosticId;

        /// <summary>
        /// Gets the identifier of the diagnostic reported for uses of the experimental API.
        /// </summary>
        public string DiagnosticId { get; }

        /// <summary>
        /// Gets or sets the URL format for the diagnostic, where <c>{0}</c> is replaced with
        /// <see cref="DiagnosticId"/>.
        /// </summary>
        public string? UrlFormat { get; set; }
    }
}
#endif
