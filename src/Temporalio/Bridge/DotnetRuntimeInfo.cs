using System;
using System.Diagnostics;

namespace Temporalio.Bridge
{
    /// <summary>
    /// Details of the .NET runtime hosting this process, reported to Core for inclusion in worker
    /// environment heartbeats.
    /// </summary>
    internal static class DotnetRuntimeInfo
    {
        private const string DotnetCoreCoreLibName = "System.Private.CoreLib";

        /// <summary>
        /// Gets the kind of .NET runtime hosting this process, or
        /// <see cref="Interop.TemporalCoreRuntimeType.Unspecified" /> if it cannot be determined.
        /// </summary>
        public static Interop.TemporalCoreRuntimeType RuntimeType { get; } = DetectRuntimeType();

        /// <summary>
        /// Gets the version of the runtime named by <see cref="RuntimeType" />, or an empty string
        /// if it cannot be determined.
        /// </summary>
        public static string Version { get; } = DetectRuntimeVersion(DetectRuntimeType());

        /// <summary>
        /// Determine the hosting runtime from CoreLib's simple assembly name.
        /// </summary>
        /// <param name="coreLibSimpleName">Simple name of the assembly declaring
        /// <see cref="object" />.</param>
        /// <returns>Runtime kind implied by the name.</returns>
        internal static Interop.TemporalCoreRuntimeType RuntimeTypeOf(string? coreLibSimpleName)
        {
            // .NET Core renamed CoreLib; .NET Framework and Mono kept mscorlib.
            return coreLibSimpleName == DotnetCoreCoreLibName ?
                Interop.TemporalCoreRuntimeType.DotnetCore :
                Interop.TemporalCoreRuntimeType.DotnetFramework;
        }

        /// <summary>
        /// Determine the version of the given runtime, as version numbers only, or an empty string
        /// if it cannot be determined.
        /// </summary>
        /// <param name="runtimeType">Runtime to report the version of.</param>
        /// <returns>Version numbers only, or an empty string.</returns>
        internal static string DetectRuntimeVersion(Interop.TemporalCoreRuntimeType runtimeType)
        {
            // Normalized here rather than per-source so every runtime reports numbers only. .NET
            // Framework needs the file version because Environment.Version reports 4.0.30319 on
            // every 4.x. Kept separate from RuntimeType so a failed read still reports the runtime.
            try
            {
                return NumericVersionPrefix(runtimeType switch
                {
                    Interop.TemporalCoreRuntimeType.DotnetCore => Environment.Version.ToString(),
                    Interop.TemporalCoreRuntimeType.DotnetFramework =>
                        FileProductVersion(typeof(object).Assembly.Location),
                    _ => string.Empty,
                });
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                return string.Empty;
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Read the product version recorded in an assembly file, which is free-form and may carry
        /// a label, or an empty string when it cannot be read.
        /// </summary>
        /// <param name="assemblyLocation">Assembly file to read, empty when unavailable.</param>
        /// <returns>Product version as recorded, or an empty string.</returns>
        internal static string FileProductVersion(string assemblyLocation)
        {
            // An empty location (single-file apps) is rejected first because GetVersionInfo throws
            // on it rather than reporting nothing.
            if (assemblyLocation.Length == 0)
            {
                return string.Empty;
            }
            try
            {
                return FileVersionInfo.GetVersionInfo(assemblyLocation).ProductVersion ??
                    string.Empty;
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                return string.Empty;
            }
#pragma warning restore CA1031
        }

        /// <summary>
        /// Take the leading run of version numbers from a version string, dropping any trailing
        /// label.
        /// </summary>
        /// <param name="version">Version string, possibly labeled.</param>
        /// <returns>Leading dotted-numeric run, or an empty string if there is none.</returns>
        internal static string NumericVersionPrefix(string version)
        {
            // A product version is free-form and routinely carries a label the heartbeat should not
            // report: SemVer prerelease and build metadata on .NET Core, or a trailing note such as
            // "4.8.9037.0 built by: NET48REL1" on some Windows builds.
            var end = 0;
            while (end < version.Length &&
                ((version[end] >= '0' && version[end] <= '9') || version[end] == '.'))
            {
                end++;
            }
            while (end > 0 && version[end - 1] == '.')
            {
                end--;
            }
            return end == version.Length ? version : version.Substring(0, end);
        }

        private static Interop.TemporalCoreRuntimeType DetectRuntimeType()
        {
            // Only the netstandard2.0 asset can be loaded by either runtime, so the others are known
            // at compile time and skip a reflection call that can fail.
#if NETFRAMEWORK
            return Interop.TemporalCoreRuntimeType.DotnetFramework;
#elif NETCOREAPP
            return Interop.TemporalCoreRuntimeType.DotnetCore;
#else
            // A throw here would become a TypeInitializationException that the CLR caches,
            // permanently failing runtime creation over telemetry.
            try
            {
                return RuntimeTypeOf(typeof(object).Assembly.GetName().Name);
            }
#pragma warning disable CA1031
            catch (Exception)
            {
                return Interop.TemporalCoreRuntimeType.Unspecified;
            }
#pragma warning restore CA1031
#endif
        }
    }
}
