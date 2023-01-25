using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflow
{
    /// <summary>
    /// Designate a method as a signal handler.
    /// </summary>
    /// <remarks>
    /// This is not inherited, so if a method is overridden, it must also have this attribute. The
    /// method must return a task (not a task with a type embedded).
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class WorkflowSignalAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// default name.
        /// </summary>
        /// <seealso cref="Name" />
        public WorkflowSignalAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WorkflowSignalAttribute"/> class with the
        /// given name.
        /// </summary>
        /// <param name="name">Workflow signal name to use.</param>
        public WorkflowSignalAttribute(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Gets or sets the workflow signal name. If this is unset, it defaults to the unqualified
        /// method name. If the method name ends with "Async", that is trimmed off when creating the
        /// default.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Signal definition.
        /// </summary>
        /// <param name="Name">Name of the signal.</param>
        /// <param name="Method">Method for the signal handler.</param>
        internal record Definition(string Name, MethodInfo Method)
        {
            private static readonly ConcurrentDictionary<MethodInfo, Definition> Definitions = new();

            /// <summary>
            /// Get a signal definition from a method or fail. The result is cached.
            /// </summary>
            /// <param name="method">Signal method.</param>
            /// <returns>Signal definition.</returns>
            public static Definition FromMethod(MethodInfo method)
            {
                return Definitions.GetOrAdd(method, CreateFromMethod);
            }

            private static Definition CreateFromMethod(MethodInfo method)
            {
                var attr = method.GetCustomAttribute<WorkflowSignalAttribute>(false) ??
                    throw new ArgumentException($"{method} missing WorkflowSignal attribute");
                // Method must only return a Task (not a subclass thereof)
                if (method.ReturnType != typeof(Task))
                {
                    throw new ArgumentException($"WorkflowSignal method {method} must return Task");
                }
                else if (!method.IsPublic)
                {
                    throw new ArgumentException($"WorkflowSignal method {method} must be public");
                }
                var name = attr.Name;
                if (name == null)
                {
                    name = method.Name;
                    // Trim trailing "Async" if that's not just the full name
                    if (name.Length > 5 && name.EndsWith("Async"))
                    {
                        name = name.Substring(0, name.Length - 5);
                    }
                }
                return new(name, method);
            }
        }
    }
}
