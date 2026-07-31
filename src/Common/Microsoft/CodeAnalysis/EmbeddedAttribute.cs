using System;

namespace Microsoft.CodeAnalysis
{
    /// <summary>
    /// Hides the polyfills in this folder from referencing compilations. Each assembly gets its own
    /// internal copy, so a project with InternalsVisibleTo to several of them would otherwise see
    /// the same type from every one and fail with CS0433.
    /// </summary>
    [Embedded]
    [AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = false)]
    internal sealed class EmbeddedAttribute : Attribute
    {
    }
}
