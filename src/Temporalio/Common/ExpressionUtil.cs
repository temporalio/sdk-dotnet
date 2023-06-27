using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Temporalio.Common
{
    /// <summary>
    /// Utilities for expression trees.
    /// </summary>
    internal static class ExpressionUtil
    {
        /// <summary>
        /// Extract method and args from expression lambda.
        /// </summary>
        /// <param name="expr">Expression.</param>
        /// <returns>Method and args.</returns>
        public static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall(
            Expression<Action> expr) => ExtractCallInternal(expr);

        /// <summary>
        /// Extract method and args from expression lambda.
        /// </summary>
        /// <typeparam name="TInstance">Instance type.</typeparam>
        /// <param name="expr">Expression.</param>
        /// <returns>Method and args.</returns>
        public static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall<TInstance>(
            Expression<Action<TInstance>> expr) => ExtractCallInternal(expr);

        /// <summary>
        /// Extract method and args from expression lambda.
        /// </summary>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="expr">Expression.</param>
        /// <returns>Method and args.</returns>
        public static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall<TResult>(
            Expression<Func<TResult>> expr) => ExtractCallInternal(expr);

        /// <summary>
        /// Extract method and args from expression lambda.
        /// </summary>
        /// <typeparam name="TInstance">Instance type.</typeparam>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="expr">Expression.</param>
        /// <param name="errorSaysPropertyAccepted">True if error should mention property can be
        /// accepted.</param>
        /// <returns>Method and args.</returns>
        public static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCall<TInstance, TResult>(
            Expression<Func<TInstance, TResult>> expr, bool errorSaysPropertyAccepted = false) =>
            ExtractCallInternal(expr, errorSaysPropertyAccepted);

        /// <summary>
        /// Extract member access or null.
        /// </summary>
        /// <typeparam name="TInstance">Instance type.</typeparam>
        /// <typeparam name="TResult">Result type.</typeparam>
        /// <param name="expr">Lambda expression.</param>
        /// <returns>Member info if any.</returns>
        public static MemberInfo? ExtractMemberAccess<TInstance, TResult>(
            Expression<Func<TInstance, TResult>> expr) =>
            (expr.Body as MemberExpression)?.Member;

        private static (MethodInfo Method, IReadOnlyCollection<object?> Args) ExtractCallInternal<TDelegate>(
            Expression<TDelegate> expr, bool errorSaysPropertyAccepted = false)
        {
            // Body must be a method call
            if (expr.Body is not MethodCallExpression call)
            {
                if (errorSaysPropertyAccepted)
                {
                    throw new ArgumentException("Expression must be a single method call or property access");
                }
                throw new ArgumentException("Expression must be a single method call");
            }
            // The LHS of the method, if non-static, must be the parameter
            if (call.Object == null && expr.Parameters.Count > 0)
            {
                throw new ArgumentException("Static call expression must not have a lambda parameter");
            }
            if (call.Object != null)
            {
                if (expr.Parameters.Count != 1 ||
                    expr.Parameters.Single() is not ParameterExpression paramExpr ||
                    paramExpr.IsByRef ||
                    call.Object != paramExpr)
                {
                    throw new ArgumentException(
                        "Instance call expression must have a single lambda parameter used for the call");
                }
            }
            // Extract all arguments. If they are constant expressions we'll optimize and just pull
            // them out, but if they are not, we will compile them live (accepting the performance
            // hit at this time).
            var args = call.Arguments.Select(e =>
            {
                if (e is ConstantExpression constExpr)
                {
                    return constExpr.Value;
                }
                var expr = Expression.Lambda<Func<object?>>(Expression.Convert(e, typeof(object)));

                // For our use case, we always want to use interpretation if we can
#if NET471_OR_GREATER
                return expr.Compile(true)();
#else
                return expr.Compile()();
#endif
            });
            return (call.Method, args.ToArray());
        }
    }
}