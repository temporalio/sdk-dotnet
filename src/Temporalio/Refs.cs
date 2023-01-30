#pragma warning disable SA1402

using System;
using Castle.DynamicProxy;

namespace Temporalio
{
    /// <summary>
    /// Static helper for creating an instance of a type to reference its methods.
    /// </summary>
    /// <typeparam name="T">Type to create an instance of.</typeparam>
    public static class Refs<T>
        where T : class
    {
        private static readonly ProxyGenerator Generator = new();

        /// <summary>
        /// Interface implemented by every ref proxy.
        /// </summary>
        public interface IProxy
        {
        }

        /// <summary>
        /// Gets an instance of this type. Only interfaces and classes with 0 argument constructors
        /// can be created.
        /// </summary>
        public static T Instance
        {
            get
            {
                // TODO(cretz): Caching?
                var type = typeof(T);
                if (type.IsInterface)
                {
                    return (T)Generator.CreateInterfaceProxyWithoutTarget(
                        type, new Type[] { typeof(IProxy) }, AlwaysFailInterceptor.Instance);
                }
                else if (type.IsClass)
                {
                    return (T)Generator.CreateClassProxy(
                        type, new Type[] { typeof(IProxy) }, AlwaysFailInterceptor.Instance);
                }
                throw new InvalidOperationException($"{type} is not a class or interface");
            }
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

    /// <summary>
    /// Internal helpers for Refs.
    /// </summary>
    internal static class Refs
    {
        /// <summary>
        /// For the given type, get the underlying proxied type if any.
        /// </summary>
        /// <param name="type">Type that may be proxied.</param>
        /// <returns>Unproxied type if proxied, otherwise just the given type.</returns>
        internal static Type GetUnproxiedType(Type type)
        {
            if (typeof(IProxyTargetAccessor).IsAssignableFrom(type))
            {
                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.Namespace == "Temporalio" && iface.Name == "IProxy")
                    {
                        return iface.GenericTypeArguments[0];
                    }
                }
            }
            return type;
        }
    }
}
