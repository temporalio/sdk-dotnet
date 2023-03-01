using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Designate a method as a query handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must be a non-async method (i.e. cannot return a Task) and must return a non-void
    /// value.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class WorkflowQueryAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryAttribute"/> class with the
        /// default name. See <see cref="Name" />.
        /// </summary>
        public WorkflowQueryAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowQueryAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow query name to use. See <see cref="Name" />.</param>
        public WorkflowQueryAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets the workflow query name. If this is unset, it defaults to the unqualified method
        /// name.
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// Query definition.
        /// </summary>
        /// <param name="Name">Name of the query.</param>
        /// <param name="Method">Method for the query handler.</param>
        internal record Definition(string Name, MethodInfo Method)
        {
            private static readonly ConcurrentDictionary<MethodInfo, Definition> Definitions = new();

            /// <summary>
            /// Get a query definition from a method or fail. The result is cached.
            /// </summary>
            /// <param name="method">Query method.</param>
            /// <returns>Query definition.</returns>
            public static Definition FromMethod(MethodInfo method)
            {
                return Definitions.GetOrAdd(method, CreateFromMethod);
            }

            private static Definition CreateFromMethod(MethodInfo method)
            {
                var attr = method.GetCustomAttribute<WorkflowQueryAttribute>(false) ??
                    throw new ArgumentException($"{method} missing WorkflowQuery attribute");
                // Method must not return void or a Task
                if (method.ReturnType == typeof(void))
                {
                    throw new ArgumentException($"WorkflowQuery method {method} must return a value");
                }
                else if (typeof(Task).IsAssignableFrom(method.ReturnType))
                {
                    throw new ArgumentException($"WorkflowQuery method {method} cannot return a Task");
                }
                else if (!method.IsPublic)
                {
                    throw new ArgumentException($"WorkflowQuery method {method} must be public");
                }
                return new(attr.Name ?? method.Name, method);
            }
        }
    }
}
