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

        private WorkflowSignalDefinition(
            string? name,
            string? description,
            MethodInfo? method,
            Delegate? del,
            HandlerUnfinishedPolicy unfinishedPolicy)
        {
            Name = name;
            Description = description;
            Method = method;
            Delegate = del;
            UnfinishedPolicy = unfinishedPolicy;
        }

        /// <summary>
        /// Gets the signal name. This is null if the signal is dynamic.
        /// </summary>
        public string? Name { get; private init; }

        /// <summary>
        /// Gets the optional signal description.
        /// </summary>
        /// <remarks>WARNING: This setting is experimental.</remarks>
        public string? Description { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the signal is dynamic.
        /// </summary>
        public bool Dynamic => Name == null;

        /// <summary>
        /// Gets the signal method if done with attribute.
        /// </summary>
        internal MethodInfo? Method { get; private init; }

        /// <summary>
        /// Gets the signal method if done with delegate.
        /// </summary>
        internal Delegate? Delegate { get; private init; }

        /// <summary>
        /// Gets the unfinished policy.
        /// </summary>
        internal HandlerUnfinishedPolicy UnfinishedPolicy { get; private init; }

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
        /// <param name="name">Signal name. Null for dynamic signal.</param>
        /// <param name="del">Signal delegate.</param>
        /// <param name="unfinishedPolicy">Actions taken if a workflow exits with a running instance
        /// of this handler.</param>
        /// <param name="description">Optional description. WARNING: This setting is experimental.
        /// </param>
        /// <returns>Signal definition.</returns>
        public static WorkflowSignalDefinition CreateWithoutAttribute(
            string? name,
            Delegate del,
            HandlerUnfinishedPolicy unfinishedPolicy = HandlerUnfinishedPolicy.WarnAndAbandon,
            string? description = null)
        {
            AssertValid(del.Method, dynamic: name == null);
            return new(name, description, null, del, unfinishedPolicy);
        }

        /// <summary>
        /// Gets the signal name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="method">Method to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromMethodForCall(MethodInfo method)
        {
            var defn = FromMethod(method);
            return defn.Name ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic signal");
        }

        private static WorkflowSignalDefinition CreateFromMethod(MethodInfo method)
        {
            var attr = method.GetCustomAttribute<WorkflowSignalAttribute>(false) ??
                throw new ArgumentException($"{method} missing WorkflowSignal attribute");
            AssertValid(method, attr.Dynamic);
            var name = attr.Name;
            if (attr.Dynamic && name != null)
            {
                throw new ArgumentException($"WorkflowSignal method {method} cannot be dynamic with custom name");
            }
            else if (!attr.Dynamic && name == null)
            {
                name = method.Name;
                // Trim trailing "Async" if that's not just the full name
                if (name.Length > 5 && name.EndsWith("Async"))
                {
                    name = name.Substring(0, name.Length - 5);
                }
            }
            return new(name, attr.Description, method, null, attr.UnfinishedPolicy);
        }

        private static void AssertValid(MethodInfo method, bool dynamic)
        {
            // Method must only return a Task (not a subclass thereof)
            if (method.ReturnType != typeof(Task))
            {
                throw new ArgumentException($"WorkflowSignal method {method} must return Task");
            }
            // If it's dynamic, must have specific signature
            if (dynamic && !WorkflowDefinition.HasValidDynamicParameters(method, requireNameFirst: true))
            {
                throw new ArgumentException(
                    $"WorkflowSignal method {method} must accept string and an array of IRawValue");
            }
        }
    }
}