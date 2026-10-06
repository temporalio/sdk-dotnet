#pragma warning disable SA1402, SA1649

// The compiler recognizes these by name, so internal copies give older targets the same nullable
// flow analysis that newer BCLs provide publicly. Each is only compiled where the BCL lacks it to
// avoid conflicting with the BCL type, and each is embedded so assemblies seeing this one via
// InternalsVisibleTo do not get an ambiguous reference against their own BCL's copy.
namespace System.Diagnostics.CodeAnalysis
{
#if !NETCOREAPP3_0_OR_GREATER
    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue" />, the parameter may be null
    /// even if the corresponding type disallows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    [Microsoft.CodeAnalysis.Embedded]
    internal sealed class MaybeNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MaybeNullWhenAttribute"/> class.
        /// </summary>
        /// <param name="returnValue">Return value condition.</param>
        public MaybeNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>
        /// Gets a value indicating whether the return value condition is true or false.
        /// </summary>
        public bool ReturnValue { get; }
    }

    /// <summary>
    /// Specifies that when a method returns <see cref="ReturnValue" />, the parameter will not be
    /// null even if the corresponding type allows it.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter, Inherited = false)]
    [Microsoft.CodeAnalysis.Embedded]
    internal sealed class NotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotNullWhenAttribute"/> class.
        /// </summary>
        /// <param name="returnValue">Return value condition.</param>
        public NotNullWhenAttribute(bool returnValue) => ReturnValue = returnValue;

        /// <summary>
        /// Gets a value indicating whether the return value condition is true or false.
        /// </summary>
        public bool ReturnValue { get; }
    }
#endif

#if !NET5_0_OR_GREATER
    /// <summary>
    /// Specifies that the method or property will ensure that the listed member is not null when
    /// returning with the specified return value condition.
    /// </summary>
    [AttributeUsage(
        AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
    [Microsoft.CodeAnalysis.Embedded]
    internal sealed class MemberNotNullWhenAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MemberNotNullWhenAttribute"/> class.
        /// </summary>
        /// <param name="returnValue">Return value condition.</param>
        /// <param name="member">Member that is not null when the condition is met.</param>
        public MemberNotNullWhenAttribute(bool returnValue, string member)
        {
            ReturnValue = returnValue;
            Member = member;
        }

        /// <summary>
        /// Gets a value indicating whether the return value condition is true or false.
        /// </summary>
        public bool ReturnValue { get; }

        /// <summary>
        /// Gets the member that is not null when the condition is met.
        /// </summary>
        public string Member { get; }
    }
#endif
}
