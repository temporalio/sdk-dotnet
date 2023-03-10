using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Definition of a workflow query.
    /// </summary>
    public class WorkflowQueryDefinition
    {
        private static readonly ConcurrentDictionary<MethodInfo, WorkflowQueryDefinition> Definitions = new();

        private WorkflowQueryDefinition(string name, MethodInfo? method, Delegate? del)
        {
            Name = name;
            Method = method;
            Delegate = del;
        }

        /// <summary>
        /// Gets the query name.
        /// </summary>
        public string Name { get; private init; }

        /// <summary>
        /// Gets the query method.
        /// </summary>
        internal MethodInfo? Method { get; private init; }

        /// <summary>
        /// Gets the query method if done with delegate.
        /// </summary>
        internal Delegate? Delegate { get; private init; }

        /// <summary>
        /// Get a query definition from a method or fail. The result is cached.
        /// </summary>
        /// <param name="method">Query method.</param>
        /// <returns>Query definition.</returns>
        public static WorkflowQueryDefinition FromMethod(MethodInfo method)
        {
            if (!method.IsPublic)
            {
                throw new ArgumentException($"WorkflowQuery method {method} must be public");
            }
            return Definitions.GetOrAdd(method, CreateFromMethod);
        }

        /// <summary>
        /// Creates a query definition from an explicit name and method. Most users should use
        /// <see cref="FromMethod" /> with attributes instead.
        /// </summary>
        /// <param name="name">Query name.</param>
        /// <param name="del">Query delegate.</param>
        /// <returns>Query definition.</returns>
        public static WorkflowQueryDefinition CreateWithoutAttribute(string name, Delegate del)
        {
            AssertValid(del.Method);
            return new(name, null, del);
        }

        private static WorkflowQueryDefinition CreateFromMethod(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<WorkflowQueryAttribute>(false) ??
                throw new ArgumentException($"{method} missing WorkflowQuery attribute");
            AssertValid(method);
            return new(attr.Name ?? method.Name, method, null);
        }

        private static void AssertValid(MethodInfo method)
        {
            // Method must not return void or a Task
            if (method.ReturnType == typeof(void))
            {
                throw new ArgumentException($"WorkflowQuery method {method} must return a value");
            }
            else if (typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new ArgumentException($"WorkflowQuery method {method} cannot return a Task");
            }
        }
    }
}