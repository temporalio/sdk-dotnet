using System;

namespace Temporalio
{
    public class Runtime
    {
        public const string Version = "0.1.0-alpha1";

        private static readonly Lazy<Runtime> _default =
            new(() => new Runtime(new Bridge.Runtime()));
        public static Runtime Default => _default.Value;

        internal readonly Bridge.Runtime runtime;

        private Runtime(Bridge.Runtime runtime)
        {
            this.runtime = runtime;
        }
    }
}
