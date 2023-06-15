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
        private static readonly ConcurrentDictionary<MethodInfo, WorkflowQueryDefinition> MethodDefinitions = new();
        private static readonly ConcurrentDictionary<PropertyInfo, WorkflowQueryDefinition> PropertyDefinitions = new();

        private WorkflowQueryDefinition(string? name, MethodInfo? method, Delegate? del)
        {
            Name = name;
            Method = method;
            Delegate = del;
        }

        /// <summary>
        /// Gets the query name. This is null if the query is dynamic.
        /// </summary>
        public string? Name { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the query is dynamic.
        /// </summary>
        public bool Dynamic => Name == null;

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
            if (method.IsStatic)
            {
                throw new ArgumentException($"WorkflowQuery method {method} cannot be static");
            }
            return MethodDefinitions.GetOrAdd(method, CreateFromMethod);
        }

        /// <summary>
        /// Get a query definition from a property getter or fail. The result is cached.
        /// </summary>
        /// <param name="property">Query property.</param>
        /// <returns>Query definition.</returns>
        public static WorkflowQueryDefinition FromProperty(PropertyInfo property) =>
            PropertyDefinitions.GetOrAdd(property, _ =>
            {
                var attr = property.GetCustomAttribute<WorkflowQueryAttribute>(false) ??
                    throw new ArgumentException($"{property} missing WorkflowQuery attribute");
                var method = property.GetGetMethod();
                if (method == null)
                {
                    throw new ArgumentException($"WorkflowQuery property {property} must have public getter");
                }
                else if (method.IsStatic)
                {
                    throw new ArgumentException($"WorkflowQuery property {property} cannot be static");
                }
                else if (attr.Dynamic)
                {
                    throw new ArgumentException($"WorkflowQuery property {property} cannot be dynamic");
                }
                return new(attr.Name ?? property.Name, method, null);
            });

        /// <summary>
        /// Creates a query definition from an explicit name and method. Most users should use
        /// <see cref="FromMethod" /> with attributes instead.
        /// </summary>
        /// <param name="name">Query name. Null for dynamic query.</param>
        /// <param name="del">Query delegate.</param>
        /// <returns>Query definition.</returns>
        public static WorkflowQueryDefinition CreateWithoutAttribute(string? name, Delegate del)
        {
            AssertValid(del.Method, dynamic: name == null);
            return new(name, null, del);
        }

        /// <summary>
        /// Gets the query name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="method">Method to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromMethodForCall(MethodInfo method)
        {
            var defn = FromMethod(method);
            return defn.Name ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic query");
        }

        /// <summary>
        /// Gets the query name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="property">Property to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromPropertyForCall(PropertyInfo property)
        {
            var defn = FromProperty(property);
            return defn.Name ??
                throw new ArgumentException(
                    $"{property} cannot be used directly since it is a dynamic query");
        }

        private static WorkflowQueryDefinition CreateFromMethod(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<WorkflowQueryAttribute>(false) ??
                throw new ArgumentException($"{method} missing WorkflowQuery attribute");
            AssertValid(method, attr.Dynamic);
            var name = attr.Name;
            if (attr.Dynamic && name != null)
            {
                throw new ArgumentException($"WorkflowQuery method {method} cannot be dynamic with custom name");
            }
            else if (!attr.Dynamic && name == null)
            {
                name = method.Name;
            }
            return new(name, method, null);
        }

        private static void AssertValid(MethodInfo method, bool dynamic)
        {
            // Method must not return void or a Task
            if (method.ReturnType == typeof(void))
            {
                throw new ArgumentException($"WorkflowQuery method {method} must return a value");
            }
            if (typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new ArgumentException($"WorkflowQuery method {method} cannot return a Task");
            }
            // If it's dynamic, must have specific signature
            if (dynamic && !WorkflowDefinition.HasValidDynamicParameters(method, requireNameFirst: true))
            {
                throw new ArgumentException(
                    $"WorkflowQuery method {method} must accept string and an array of IRawValue");
            }
        }
    }
}