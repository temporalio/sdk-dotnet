using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Temporalio.Workflows
{
    /// <summary>
    /// Definition of a workflow.
    /// </summary>
    public class WorkflowDefinition
    {
        private static readonly ConcurrentDictionary<Type, WorkflowDefinition> Definitions = new();

        private readonly Func<object?[], object>? creator;

        private WorkflowDefinition(
            string? name,
            Type type,
            MethodInfo runMethod,
            Func<object?[], object>? creator,
            Type[]? failureExceptionTypes,
            IReadOnlyDictionary<string, WorkflowSignalDefinition> signals,
            WorkflowSignalDefinition? dynamicSignal,
            IReadOnlyDictionary<string, WorkflowQueryDefinition> queries,
            WorkflowQueryDefinition? dynamicQuery,
            IReadOnlyDictionary<string, WorkflowUpdateDefinition> updates,
            WorkflowUpdateDefinition? dynamicUpdate)
        {
            Name = name;
            Type = type;
            RunMethod = runMethod;
            this.creator = creator;
            FailureExceptionTypes = failureExceptionTypes;
            Signals = signals;
            DynamicSignal = dynamicSignal;
            Queries = queries;
            DynamicQuery = dynamicQuery;
            Updates = updates;
            DynamicUpdate = dynamicUpdate;
        }

        /// <summary>
        /// Gets the workflow name or null if workflow is dynamic.
        /// </summary>
        public string? Name { get; private init; }

        /// <summary>
        /// Gets the workflow type.
        /// </summary>
        public Type Type { get; private init; }

        /// <summary>
        /// Gets the workflow entry point.
        /// </summary>
        public MethodInfo RunMethod { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the workflow type can be created and used in a worker.
        /// </summary>
        public bool Instantiable => creator != null;

        /// <summary>
        /// Gets the signals for the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, WorkflowSignalDefinition> Signals { get; private init; }

        /// <summary>
        /// Gets the dynamic signal for the workflow.
        /// </summary>
        public WorkflowSignalDefinition? DynamicSignal { get; private init; }

        /// <summary>
        /// Gets the queries for the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, WorkflowQueryDefinition> Queries { get; private init; }

        /// <summary>
        /// Gets the dynamic query for the workflow.
        /// </summary>
        public WorkflowQueryDefinition? DynamicQuery { get; private init; }

        /// <summary>
        /// Gets the updates for the workflow.
        /// </summary>
        public IReadOnlyDictionary<string, WorkflowUpdateDefinition> Updates { get; private init; }

        /// <summary>
        /// Gets the dynamic update for the workflow.
        /// </summary>
        public WorkflowUpdateDefinition? DynamicUpdate { get; private init; }

        /// <summary>
        /// Gets a value indicating whether the workflow is dynamic.
        /// </summary>
        public bool Dynamic => Name == null;

        /// <summary>
        /// Gets the failure exception types. See
        /// <see cref="WorkflowAttribute.FailureExceptionTypes" /> for more details.
        /// </summary>
        public Type[]? FailureExceptionTypes { get; private init; }

        /// <summary>
        /// Create a workflow definition for the given type or fail. The result is cached by type.
        /// </summary>
        /// <typeparam name="T">Type to get definition for.</typeparam>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition Create<T>() => Create(typeof(T));

        /// <summary>
        /// Create a workflow definition for the given type or fail. The result is cached by type.
        /// </summary>
        /// <param name="type">Type to get definition for.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition Create(Type type) =>
            Definitions.GetOrAdd(type, type => Create(type, null, null));

        /// <summary>
        /// Create a workflow with a custom creator. The result is not cached. Most users will use
        /// <see cref="Create(Type)" /> instead.
        /// </summary>
        /// <param name="type">Type to get definition for.</param>
        /// <param name="nameOverride">The name to use instead of what may be on the attribute.</param>
        /// <param name="creatorOverride">If present, the method to use to create an instance of
        /// the workflow.</param>
        /// <returns>Definition for the type.</returns>
        public static WorkflowDefinition Create(
            Type type, string? nameOverride, Func<object?[], object>? creatorOverride)
        {
            // We will keep track of errors and only throw an aggregate at the end
            var errs = new List<string>();

            // Get the main attribute or throw if not present
            var attr = type.GetCustomAttribute<WorkflowAttribute>(false) ??
                throw new ArgumentException($"{type} missing Workflow attribute");

            // Use override or attr name or fall back to type name (cannot have name with dynamic)
            var name = nameOverride ?? attr.Name;
            if (!attr.Dynamic && name == null)
            {
                name = type.Name;
                // If type is an interface and name has a leading I followed by another capital,
                // trim it off
                if (type.IsInterface && name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
                {
                    name = name.Substring(1);
                }
            }
            else if (attr.Dynamic && name != null)
            {
                errs.Add("Cannot have custom name for dynamic workflow");
            }
            if (name != null && name.Contains('\n'))
            {
                errs.Add("Workflow name cannot have a newline");
            }

            const BindingFlags bindingFlagsAny =
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.Public | BindingFlags.NonPublic;

            // No generics allowed currently
            if (type.GenericTypeArguments.Length > 0)
            {
                errs.Add($"{type} has generic type arguments");
            }

            // Check constructors. We intentionally fetch non-public too to make sure the init
            // attribute isn't set on them.
            ConstructorInfo? initConstructor = null;
            var constructors = type.GetConstructors(bindingFlagsAny);
            var hasParameterlessConstructor = constructors.Length == 0;
            foreach (var constructor in constructors)
            {
                if (constructor.GetParameters().Length == 0)
                {
                    hasParameterlessConstructor = true;
                }
                var initAttr = constructor.GetCustomAttribute<WorkflowInitAttribute>(false);
                if (initAttr != null)
                {
                    if (initConstructor != null)
                    {
                        errs.Add($"WorkflowInit on multiple: {constructor} and {initConstructor}");
                    }
                    else if (!constructor.IsPublic)
                    {
                        errs.Add($"WorkflowInit on non-public {constructor}");
                    }
                    else
                    {
                        initConstructor = constructor;
                    }
                }
            }
            // If an creator is provided, must not have a workflow init constructor
            var creator = creatorOverride;
            if (initConstructor != null)
            {
                if (creator != null)
                {
                    throw new ArgumentException(
                        "Cannot set creator for workflow with WorkflowInit constructor",
                        nameof(creatorOverride));
                }
                creator = initConstructor.Invoke;
            }
            else if (creator == null && hasParameterlessConstructor && !type.IsInterface)
            {
                creator = _ => Activator.CreateInstance(type)!;
            }

            // We need to pre-collect the workflow update validators so they can be popped off later
            var updateValidators = new Dictionary<string, MethodInfo>();
            foreach (var method in type.GetMethods(bindingFlagsAny))
            {
                var validatorAttr = method.GetCustomAttribute<WorkflowUpdateValidatorAttribute>();
                if (validatorAttr == null)
                {
                    continue;
                }
                else if (updateValidators.ContainsKey(validatorAttr.UpdateMethod))
                {
                    errs.Add($"{type} has more than one update validator for update method {validatorAttr.UpdateMethod}");
                }
                else if (method.ContainsGenericParameters)
                {
                    errs.Add($"{method} with WorkflowUpdateValidator contains generic parameters");
                }
                else
                {
                    updateValidators[validatorAttr.UpdateMethod] = method;
                }
            }

            // Find and validate run, signal, query, and update methods. We intentionally fetch
            // non-public too to make sure attributes aren't set on them.
            MethodInfo? runMethod = null;
            var signals = new Dictionary<string, WorkflowSignalDefinition>();
            WorkflowSignalDefinition? dynamicSignal = null;
            var queries = new Dictionary<string, WorkflowQueryDefinition>();
            WorkflowQueryDefinition? dynamicQuery = null;
            var updates = new Dictionary<string, WorkflowUpdateDefinition>();
            WorkflowUpdateDefinition? dynamicUpdate = null;
            foreach (var method in type.GetMethods(bindingFlagsAny))
            {
                var runAttr = method.GetCustomAttribute<WorkflowRunAttribute>(false);
                if (runAttr != null)
                {
                    if (method.DeclaringType != type)
                    {
                        errs.Add($"WorkflowRun on {method} must be declared on {type}, not inherited from {method.DeclaringType}");
                    }
                    else if (runMethod != null)
                    {
                        errs.Add($"WorkflowRun on multiple: {method} and {runMethod}");
                    }
                    else if (!method.IsPublic)
                    {
                        errs.Add($"WorkflowRun on non-public {method}");
                    }
                    else if (method.IsStatic)
                    {
                        errs.Add($"WorkflowRun on static {method}");
                    }
                    else if (method.ContainsGenericParameters)
                    {
                        errs.Add($"{method} with WorkflowRun contains generic parameters");
                    }
                    else if (!typeof(Task).IsAssignableFrom(method.ReturnType))
                    {
                        errs.Add($"WorkflowRun method {method} must return an instance of Task");
                    }
                    else if (initConstructor != null &&
                        !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(
                            initConstructor.GetParameters().Select(p => p.ParameterType)))
                    {
                        errs.Add($"WorkflowRun on {method} must match parameter types of WorkflowInit on {initConstructor}");
                    }
                    else if (attr.Dynamic && !HasValidDynamicParameters(method, requireNameFirst: false))
                    {
                        errs.Add($"WorkflowRun on {method} for dynamic workflow must accept an array of IRawValue");
                    }
                    else
                    {
                        runMethod = method;
                    }
                }
                if (method.IsDefined(typeof(WorkflowSignalAttribute), false))
                {
                    try
                    {
                        var defn = WorkflowSignalDefinition.FromMethod(method);
                        if (defn.Name == null)
                        {
                            if (dynamicSignal != null)
                            {
                                errs.Add($"{type} has more than one dynamic signal");
                            }
                            dynamicSignal = defn;
                        }
                        else if (signals.ContainsKey(defn.Name))
                        {
                            errs.Add($"{type} has more than one signal named {defn.Name}");
                        }
                        else if (method.ContainsGenericParameters)
                        {
                            errs.Add($"{method} with WorkflowSignal contains generic parameters");
                        }
                        else
                        {
                            signals[defn.Name] = defn;
                        }
                    }
                    catch (ArgumentException e)
                    {
                        errs.Add(e.Message);
                    }
                }
                else if (IsDefinedOnBase<WorkflowSignalAttribute>(method))
                {
                    errs.Add($"WorkflowSignal on base definition of {method} but not override");
                }
                if (method.IsDefined(typeof(WorkflowQueryAttribute), false))
                {
                    try
                    {
                        var defn = WorkflowQueryDefinition.FromMethod(method);
                        if (defn.Name == null)
                        {
                            if (dynamicQuery != null)
                            {
                                errs.Add($"{type} has more than one dynamic query");
                            }
                            dynamicQuery = defn;
                        }
                        else if (queries.ContainsKey(defn.Name))
                        {
                            errs.Add($"{type} has more than one query named {defn.Name}");
                        }
                        else if (method.ContainsGenericParameters)
                        {
                            errs.Add($"{method} with WorkflowQuery contains generic parameters");
                        }
                        else
                        {
                            queries[defn.Name] = defn;
                        }
                    }
                    catch (ArgumentException e)
                    {
                        errs.Add(e.Message);
                    }
                }
                else if (IsDefinedOnBase<WorkflowQueryAttribute>(method))
                {
                    errs.Add($"WorkflowQuery on base definition of {method} but not override");
                }
                if (method.IsDefined(typeof(WorkflowUpdateAttribute), false))
                {
                    try
                    {
                        if (updateValidators.TryGetValue(method.Name, out var updateValidatorMethod))
                        {
                            updateValidators.Remove(method.Name);
                        }
                        var defn = WorkflowUpdateDefinition.FromMethod(method, updateValidatorMethod);
                        if (defn.Name == null)
                        {
                            if (dynamicUpdate != null)
                            {
                                errs.Add($"{type} has more than one dynamic update");
                            }
                            dynamicUpdate = defn;
                        }
                        else if (updates.ContainsKey(defn.Name))
                        {
                            errs.Add($"{type} has more than one update named {defn.Name}");
                        }
                        else if (method.ContainsGenericParameters)
                        {
                            errs.Add($"{method} with WorkflowUpdate contains generic parameters");
                        }
                        else
                        {
                            updates[defn.Name] = defn;
                        }
                    }
                    catch (ArgumentException e)
                    {
                        errs.Add(e.Message);
                    }
                }
                else if (IsDefinedOnBase<WorkflowUpdateAttribute>(method))
                {
                    errs.Add($"WorkflowUpdate on base definition of {method} but not override");
                }
            }
            if (runMethod == null)
            {
                errs.Add($"{type} does not have a valid WorkflowRun method");
            }

            // Each update validator that remains is a failure to assign to update method
            foreach (var kv in updateValidators)
            {
                errs.Add($"Cannot find update method named {kv.Key} for WorkflowUpdateValidator on {kv.Value} method");
            }

            // Get query attributes on properties
            foreach (var property in type.GetProperties(bindingFlagsAny))
            {
                if (property.IsDefined(typeof(WorkflowQueryAttribute), false))
                {
                    try
                    {
                        var defn = WorkflowQueryDefinition.FromProperty(property);
                        if (queries.ContainsKey(defn.Name!))
                        {
                            errs.Add($"{type} has more than one query named {defn.Name}");
                        }
                        queries[defn.Name!] = defn;
                    }
                    catch (ArgumentException e)
                    {
                        errs.Add(e.Message);
                    }
                }
            }

            // If there are any errors, throw
            if (errs.Count > 0)
            {
                throw new AggregateException(errs.Select(err => new ArgumentException(err)));
            }

            return new(
                name: name,
                type: type,
                runMethod: runMethod!,
                creator: creator,
                failureExceptionTypes: attr.FailureExceptionTypes,
                signals: signals,
                dynamicSignal: dynamicSignal,
                queries: queries,
                dynamicQuery: dynamicQuery,
                updates: updates,
                dynamicUpdate: dynamicUpdate);
        }

        /// <summary>
        /// Instantiate an instance of the workflow with the given run arguments.
        /// </summary>
        /// <param name="workflowArguments">Arguments for workflow run.</param>
        /// <returns>The created workflow instance.</returns>
        public object CreateWorkflowInstance(object?[] workflowArguments)
        {
            if (creator == null)
            {
                throw new InvalidOperationException($"Cannot instantiate workflow type {Type}");
            }
            return creator.Invoke(workflowArguments);
        }

        /// <summary>
        /// Gets the workflow name for calling or fail if no attribute or if dynamic.
        /// </summary>
        /// <param name="runMethod">Run method to get name from.</param>
        /// <returns>Name.</returns>
        internal static string NameFromRunMethodForCall(MethodInfo runMethod)
        {
            if (runMethod.GetCustomAttribute<WorkflowRunAttribute>() == null)
            {
                throw new ArgumentException($"{runMethod} missing WorkflowRun attribute");
            }
            // We intentionally use reflected type because we don't allow inheritance of run methods
            // in any way, they must be explicitly defined on the type
            var defn = Create(runMethod.ReflectedType ??
                throw new ArgumentException($"{runMethod} has no reflected type"));
            if (defn.Name == null)
            {
                throw new ArgumentException(
                    $"{runMethod} cannot be used directly since it is a dynamic workflow");
            }
            return defn.Name;
        }

        /// <summary>
        /// Get whether the given method's parameters are valid for dynamic call.
        /// </summary>
        /// <param name="method">Method to check.</param>
        /// <param name="requireNameFirst">Whether a string param must be first.</param>
        /// <returns>True if valid, false otherwise.</returns>
        internal static bool HasValidDynamicParameters(MethodInfo method, bool requireNameFirst)
        {
            var parms = method.GetParameters();
            if (requireNameFirst)
            {
                if (parms.Length != 2 || parms.First().ParameterType != typeof(string))
                {
                    return false;
                }
            }
            else if (parms.Length != 1)
            {
                return false;
            }
            return parms.Last().ParameterType == typeof(Converters.IRawValue[]);
        }

        private static bool IsDefinedOnBase<T>(MethodInfo method)
            where T : Attribute
        {
            while (true)
            {
                var baseDef = method.GetBaseDefinition();
                if (baseDef == method)
                {
                    return false;
                }
                else if (baseDef.IsDefined(typeof(T), false))
                {
                    return true;
                }
                method = baseDef;
            }
        }
    }
}