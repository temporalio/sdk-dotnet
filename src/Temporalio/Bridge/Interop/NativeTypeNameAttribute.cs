using System;
using System.Diagnostics;

// This was taken when running generate bridge with
// "--config generate-helper-types" and taking that attribute

namespace Temporalio.Bridge.Interop
{
    /// <summary>Defines the type of a member as it was used in the native signature.</summary>
    [AttributeUsage(
        AttributeTargets.Struct
            | AttributeTargets.Enum
            | AttributeTargets.Property
            | AttributeTargets.Field
            | AttributeTargets.Parameter
            | AttributeTargets.ReturnValue,
        AllowMultiple = false,
        Inherited = true
    )]
    [Conditional("DEBUG")]
    internal sealed partial class NativeTypeNameAttribute : Attribute
    {
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="NativeTypeNameAttribute" /> class.</summary>
        /// <param name="name">The name of the type that was used in the native signature.</param>
        public NativeTypeNameAttribute(string name)
        {
            _name = name;
        }

        /// <summary>Gets the name of the type that was used in the native signature.</summary>
        public string Name => _name;
    }
}
