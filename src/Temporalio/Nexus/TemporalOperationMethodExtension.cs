using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using NexusRpc;
using NexusRpc.Handlers;

namespace Temporalio.Nexus
{
    /// <summary>
    /// NexusRpc <see cref="IMethodExtension"/> that recognizes <see cref="TemporalOperationAttribute"/>
    /// methods and builds an <see cref="TemporalOperationHandler{TInput, TResult}"/> for each.
    /// </summary>
    internal sealed class TemporalOperationMethodExtension : IMethodExtension
    {
        /// <summary>
        /// Singleton instance.
        /// </summary>
        public static readonly TemporalOperationMethodExtension Instance = new();

        /// <summary>
        /// Convenience collection wrapping the singleton for passing to
        /// <c>ServiceHandlerInstance.FromInstance</c>.
        /// </summary>
        public static readonly IReadOnlyCollection<IMethodExtension> Extensions =
            new IMethodExtension[] { Instance };

        /// <inheritdoc/>
        public MethodExtensionResult? Extract(
            object instance, MethodInfo method, ServiceDefinition serviceDefinition)
        {
            if (method.GetCustomAttribute<TemporalOperationAttribute>() == null)
            {
                return null;
            }
            if (!method.IsPublic)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must be public");
            }
            if (method.IsStatic)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must not be static");
            }
            var opDef = serviceDefinition.Operations.Values
                .FirstOrDefault(o => o.MethodInfo?.Name == method.Name) ??
                throw new ArgumentException(
                    $"No matching NexusOperation on service '{serviceDefinition.Name}' for " +
                    $"[TemporalOperation] method '{method.Name}'");

            var hasInputParam =
                opDef.InputType != typeof(void) && opDef.InputType != typeof(NoValue);
            var inputType = hasInputParam ? opDef.InputType : typeof(NoValue);
            var resultType =
                opDef.OutputType == typeof(void) ? typeof(NoValue) : opDef.OutputType;

            // Expected: Task<TemporalOperationResult<TResult>> Method(
            //     TemporalOperationStartContext, ITemporalNexusClient[, TInput])
            var expectedReturn = typeof(Task<>).MakeGenericType(
                typeof(TemporalOperationResult<>).MakeGenericType(resultType));
            if (method.ReturnType != expectedReturn)
            {
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must return " +
                    $"{FormatTypeName(expectedReturn)}; got {FormatTypeName(method.ReturnType)}");
            }

            var parameters = method.GetParameters();
            var expectedParamCount = hasInputParam ? 3 : 2;
            if (parameters.Length != expectedParamCount ||
                parameters[0].ParameterType != typeof(TemporalOperationStartContext) ||
                parameters[1].ParameterType != typeof(ITemporalNexusClient) ||
                (hasInputParam && parameters[2].ParameterType != inputType))
            {
                var expected = hasInputParam
                    ? $"(TemporalOperationStartContext, ITemporalNexusClient, {FormatTypeName(inputType)})"
                    : "(TemporalOperationStartContext, ITemporalNexusClient)";
                throw new ArgumentException(
                    $"[TemporalOperation] method {method.DeclaringType}.{method.Name} must accept " +
                    $"parameters {expected}");
            }

            // Build a delegate Func<ctx, client, TInput, Task<TemporalOperationResult<TResult>>>
            // that either calls the method directly (with input) or the 2-arg method (no input).
            var startFuncType = typeof(Func<,,,>).MakeGenericType(
                typeof(TemporalOperationStartContext),
                typeof(ITemporalNexusClient),
                inputType,
                expectedReturn);
            var startFunc = BuildStartFuncDelegate(
                instance, method, inputType, startFuncType, hasInputParam);

            // Instantiate TemporalOperationHandler<TInput, TResult> and wrap as generic.
            var handlerType = typeof(TemporalOperationHandler<,>).MakeGenericType(inputType, resultType);
            var handler = Activator.CreateInstance(handlerType, startFunc)!;
            var ioHandlerType = typeof(IOperationHandler<,>).MakeGenericType(inputType, resultType);
            return new MethodExtensionResult(
                opDef.Name,
                OperationHandler.WrapAsGenericHandler(handler, ioHandlerType));
        }

        private static Delegate BuildStartFuncDelegate(
            object instance,
            MethodInfo method,
            Type inputType,
            Type funcType,
            bool hasInputParam)
        {
            var ctxParam = Expression.Parameter(typeof(TemporalOperationStartContext), "ctx");
            var clientParam = Expression.Parameter(typeof(ITemporalNexusClient), "client");
            var inputParam = Expression.Parameter(inputType, "input");
            var instanceExpr = Expression.Constant(instance, method.DeclaringType!);
            var call = hasInputParam
                ? Expression.Call(instanceExpr, method, ctxParam, clientParam, inputParam)
                : Expression.Call(instanceExpr, method, ctxParam, clientParam);
            var lambda = Expression.Lambda(funcType, call, ctxParam, clientParam, inputParam);
            // Prefer interpretation over full IL compilation: it is far cheaper for these
            // single-use lambdas by avoiding Reflection.Emit + JIT work. The compile targets
            // for net470 and lower lack the overload.
#if NETSTANDARD2_0 || NETCOREAPP || NET471_OR_GREATER
            return lambda.Compile(true);
#else
            return lambda.Compile();
#endif
        }

        private static string FormatTypeName(Type type)
        {
            if (!type.IsGenericType)
            {
                return type.Name;
            }
            var name = type.Name;
            var backtick = name.IndexOf('`');
            if (backtick >= 0)
            {
                name = name.Substring(0, backtick);
            }
            var args = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{name}<{args}>";
        }
    }
}
