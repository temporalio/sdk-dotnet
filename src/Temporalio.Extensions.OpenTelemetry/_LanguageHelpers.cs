#pragma warning disable SA1649

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Needed for init-only properties to work on older .NET versions.
    /// </summary>
    internal static class IsExternalInit
    {
    }
}
