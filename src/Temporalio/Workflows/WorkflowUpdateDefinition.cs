using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Definition of a workflow update.
    /// </summary>
    public class WorkflowUpdateDefinition
    {
        private static readonly ConcurrentDictionary<MethodInfo, WorkflowUpdateDefinition> Definitions = new();

        private WorkflowUpdateDefinition(
            string? name,
            MethodInfo? method,
            MethodInfo? validatorMethod,
            Delegate? del,
            Delegate? validatorDel,
            HandlerUnfinishedPolicy unfinishedPolicy)
        {
            Name = name;
            Method = method;
            ValidatorMethod = validatorMethod;
            Delegate = del;
            ValidatorDelegate = validatorDel;
            UnfinishedPolicy = unfinishedPolicy;
        }

        /// <summary>
        /// Gets the update name. This is null if the update is dynamic.
        /// </summary>
        public string? Name { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the update is dynamic.
        /// </summary>
        public bool Dynamic => Name == null;

        /// <summary>
        /// Gets the update method if done with an attribute.
        /// </summary>
        internal MethodInfo? Method { get; private init; }

        /// <summary>
        /// Gets the validator method if present and done with an attribute.
        /// </summary>
        internal MethodInfo? ValidatorMethod { get; private init; }

        /// <summary>
        /// Gets the update method if done with a delegate.
        /// </summary>
        internal Delegate? Delegate { get; private init; }

        /// <summary>
        /// Gets the validator method if present and done with a delegate.
        /// </summary>
        internal Delegate? ValidatorDelegate { get; private init; }

        /// <summary>
        /// Gets the unfinished policy.
        /// </summary>
        internal HandlerUnfinishedPolicy UnfinishedPolicy { get; private init; }

        /// <summary>
        /// Get an update definition from a method or fail. The result is cached.
        /// </summary>
        /// <param name="method">Update method.</param>
        /// <param name="validatorMethod">Optional validator method.</param>
        /// <returns>Update definition.</returns>
        public static WorkflowUpdateDefinition FromMethod(
            MethodInfo method, MethodInfo? validatorMethod = null)
        {
            if (!method.IsPublic)
            {
                throw new ArgumentException($"WorkflowUpdate method {method} must be public");
            }
            if (method.IsStatic)
            {
                throw new ArgumentException($"WorkflowUpdate method {method} cannot be static");
            }
            if (validatorMethod != null && !validatorMethod.IsPublic)
            {
                throw new ArgumentException($"WorkflowUpdateValidator method {validatorMethod} must be public");
            }
            if (validatorMethod != null && validatorMethod.IsStatic)
            {
                throw new ArgumentException($"WorkflowUpdateValidator method {validatorMethod} cannot be static");
            }
            return Definitions.GetOrAdd(method, method => CreateFromMethod(method, validatorMethod));
        }

        /// <summary>
        /// Creates an update definition from an explicit name and method. Most users should use
        /// <see cref="FromMethod" /> with attributes instead.
        /// </summary>
        /// <param name="name">Update name. Null for dynamic update.</param>
        /// <param name="del">Update delegate.</param>
        /// <param name="validatorDel">Optional validator delegate.</param>
        /// <param name="unfinishedPolicy">Actions taken if a workflow exits with a running instance
        /// of this handler.</param>
        /// <returns>Update definition.</returns>
        public static WorkflowUpdateDefinition CreateWithoutAttribute(
            string? name,
            Delegate del,
            Delegate? validatorDel = null,
            HandlerUnfinishedPolicy unfinishedPolicy = HandlerUnfinishedPolicy.WarnAndAbandon)
        {
            AssertValid(del.Method, dynamic: name == null, validatorDel?.Method);
            return new(name, null, null, del, validatorDel, unfinishedPolicy);
        }

        /// <summary>
        /// Gets the update name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="method">Method to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromMethodForCall(MethodInfo method)
        {
            var defn = FromMethod(method);
            return defn.Name ??
                throw new ArgumentException(
                    $"{method} cannot be used directly since it is a dynamic update");
        }

        private static WorkflowUpdateDefinition CreateFromMethod(
            MethodInfo method, MethodInfo? validatorMethod)
        {
            var attr = method.GetCustomAttribute<WorkflowUpdateAttribute>(false) ??
                throw new ArgumentException($"{method} missing WorkflowUpdate attribute");
            AssertValid(method, attr.Dynamic, validatorMethod);
            var name = attr.Name;
            if (attr.Dynamic && name != null)
            {
                throw new ArgumentException($"WorkflowUpdate method {method} cannot be dynamic with custom name");
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
            return new(name, method, validatorMethod, null, null, attr.UnfinishedPolicy);
        }

        private static void AssertValid(
            MethodInfo method, bool dynamic, MethodInfo? validatorMethod)
        {
            // Method must return a task or a subtype thereof
            if (!typeof(Task).IsAssignableFrom(method.ReturnType))
            {
                throw new ArgumentException($"WorkflowUpdate method {method} must return Task");
            }
            // If it's dynamic, must have specific signature
            if (dynamic && !WorkflowDefinition.HasValidDynamicParameters(method, requireNameFirst: true))
            {
                throw new ArgumentException(
                    $"WorkflowUpdate method {method} must accept string and an array of IRawValue");
            }
            // Do validator method checks
            if (validatorMethod is { } validatorMeth)
            {
                // Must not return a value
                if (validatorMeth.ReturnType != typeof(void))
                {
                    throw new ArgumentException($"WorkflowUpdateValidator method {validatorMeth} must be void");
                }
                // Must match signature
                if (!validatorMeth.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                    method.GetParameters().Select(p => p.ParameterType)))
                {
                    throw new ArgumentException(
                        $"WorkflowUpdateValidator method {validatorMeth} must have the same parameters as {method}");
                }
            }
        }
    }
}