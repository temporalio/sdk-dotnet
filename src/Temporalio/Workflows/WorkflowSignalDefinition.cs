using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Definition of a workflow signal.
    /// </summary>
    public class WorkflowSignalDefinition
    {
        private static readonly ConcurrentDictionary<MethodInfo, WorkflowSignalDefinition> Definitions = new();

        private WorkflowSignalDefinition(string name, MethodInfo? method, Delegate? del)
        {
            Name = name;
            Method = method;
            Delegate = del;
        }

        /// <summary>
        /// Gets the signal name.
        /// </summary>
        public string Name { get; private init; }

        /// <summary>
        /// Gets the signal method if done with attribute.
        /// </summary>
        internal MethodInfo? Method { get; private init; }

        /// <summary>
        /// Gets the signal method if done with delegate.
        /// </summary>
        internal Delegate? Delegate { get; private init; }

        /// <summary>
        /// Get a signal definition from a method or fail. The result is cached.
        /// </summary>
        /// <param name="method">Signal method.</param>
        /// <returns>Signal definition.</returns>
        public static WorkflowSignalDefinition FromMethod(MethodInfo method)
        {
            if (!method.IsPublic)
            {
                throw new ArgumentException($"WorkflowSignal method {method} must be public");
            }
            if (method.IsStatic)
            {
                throw new ArgumentException($"WorkflowSignal method {method} cannot be static");
            }
            return Definitions.GetOrAdd(method, CreateFromMethod);
        }

        /// <summary>
        /// Creates a signal definition from an explicit name and method. Most users should use
        /// <see cref="FromMethod" /> with attributes instead.
        /// </summary>
        /// <param name="name">Signal name.</param>
        /// <param name="del">Signal delegate.</param>
        /// <returns>Signal definition.</returns>
        public static WorkflowSignalDefinition CreateWithoutAttribute(string name, Delegate del)
        {
            AssertValid(del.Method);
            return new(name, null, del);
        }

        private static WorkflowSignalDefinition CreateFromMethod(MethodInfo method)
        {
            AssertValid(method);
            var attr = method.GetCustomAttribute<WorkflowSignalAttribute>(false) ??
                throw new ArgumentException($"{method} missing WorkflowSignal attribute");
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
            return new(name, method, null);
        }

        private static void AssertValid(MethodInfo method)
        {
            // Method must only return a Task (not a subclass thereof)
            if (method.ReturnType != typeof(Task))
            {
                throw new ArgumentException($"WorkflowSignal method {method} must return Task");
            }
        }
    }
}