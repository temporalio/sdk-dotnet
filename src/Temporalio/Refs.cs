#pragma warning disable SA1402

using System;
using System.Reflection;
using Castle.DynamicProxy;

namespace Temporalio
{
    /// <summary>
    /// Static helper for creating an instance of a type to reference its methods.
    /// </summary>
    public static class Refs
    {
        private static readonly ProxyGenerator Generator = new();
        private static readonly ConstructorInfo ProxiedAttributeConstructor =
            typeof(ProxiedAttribute).GetConstructor(new Type[] { typeof(Type) }) ??
                throw new InvalidOperationException("Missing proxied attribute constructor");

#pragma warning disable CA1040 // We allow this empty marker interface because
        /// <summary>
        /// Interface implemented by every ref proxy.
        /// </summary>
        public interface IProxy
        {
        }

        /// <summary>
        /// Create an instance of the given type. Only interfaces and classes with 0-argument
        /// constructors can be created. This should only be used to reference methods on, not to
        /// make any calls on.
        /// </summary>
        /// <typeparam name="T">Type to create an instance of.</typeparam>
        /// <returns>Instance of this type.</returns>
        public static T Create<T>()
        {
            var type = typeof(T);
            var options = new ProxyGenerationOptions();
            options.AdditionalAttributes.Add(
                new(ProxiedAttributeConstructor, new object[] { type }));
            if (type.IsInterface)
            {
                return (T)Generator.CreateInterfaceProxyWithoutTarget(
                    type, options, AlwaysFailInterceptor.Instance);
            }
            else if (type.IsClass)
            {
                return (T)Generator.CreateClassProxy(
                    type, options, AlwaysFailInterceptor.Instance);
            }
            throw new InvalidOperationException($"{type} is not a class or interface");
        }

        /// <summary>
        /// For the given type, get the underlying proxied type if any.
        /// </summary>
        /// <param name="type">Type that may be proxied.</param>
        /// <returns>Unproxied type if proxied, otherwise just the given type.</returns>
        internal static Type GetUnproxiedType(Type type) =>
            type.GetCustomAttribute<ProxiedAttribute>()?.UnderlyingType ?? type;

        /// <summary>
        /// Attribute present on every proxied instance type.
        /// </summary>
        [AttributeUsage(AttributeTargets.Method, Inherited = false)]
        public sealed class ProxiedAttribute : Attribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="ProxiedAttribute"/> class.
            /// </summary>
            /// <param name="underlyingType">Type that is proxied.</param>
            public ProxiedAttribute(Type underlyingType)
            {
                UnderlyingType = underlyingType;
            }

            /// <summary>
            /// Gets the underlying type that is proxied.
            /// </summary>
            public Type UnderlyingType { get; private init; }
        }

        private class AlwaysFailInterceptor : IInterceptor
        {
            internal static readonly AlwaysFailInterceptor Instance = new();

            public void Intercept(IInvocation invocation)
            {
                throw new NotImplementedException();
            }
        }
    }
}
