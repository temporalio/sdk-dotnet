#pragma warning disable SA1649

using System;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Hides the marked type from other assemblies, even those granted InternalsVisibleTo.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, Inherited = false)]
    [Embedded]
    internal sealed class EmbeddedAttribute : Attribute
    {
    }
}
