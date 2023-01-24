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
        /// Gets an instance of this type. Only interfaces and classes with 0 argument constructors
        /// can be created.
        /// </summary>
        public static T Instance
        {
            get
            {
                // TODO(cretz): Caching and more
                var type = typeof(T);
                if (type.IsInterface)
                {
                    return Generator.CreateInterfaceProxyWithoutTarget<T>(AlwaysFailInterceptor.Instance);
                }
                else if (type.IsClass)
                {
                    return Generator.CreateClassProxy<T>(AlwaysFailInterceptor.Instance);
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
}
