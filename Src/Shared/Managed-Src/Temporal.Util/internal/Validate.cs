using System;
using System.Runtime.CompilerServices;

namespace Temporal.Util
{
    internal static class Validate
    {
        private const string FallbackValidatedExpression = "specified value";

        /// <summary>
        /// Parameter check for Null.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="validatedExpression">The expression being validated (often, it is the name of the parameter being checked).</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNull(object value, [CallerArgumentExpression("value")] string validatedExpression = FallbackValidatedExpression)
        {
            if (value == null)
            {
                ThrowArgumentNullException(validatedExpression);
            }
        }

        /// <summary>
        /// String parameter check with a more informative exception that specifies whether
        /// the problem was that the string was null or empty.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="validatedExpression">The expression being validated (often, it is the name of the parameter being checked).</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        /// <exception cref="ArgumentException">If the value is an empty string.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrEmpty(string value, [CallerArgumentExpression("value")] string validatedExpression = FallbackValidatedExpression)
        {
            if (value == null)
            {
                ThrowArgumentNullException(validatedExpression);
            }

            if (value.Length == 0)
            {
                ThrowArgumentException(validatedExpression, " may not be empty.");
            }
        }

        /// <summary>
        /// String parameter check with a more informative exception that specifies whether
        /// the problem was that the string was null, empty or whitespace only.
        /// </summary>
        /// <param name="value">Value to be checked.</param>
        /// <param name="validatedExpression">The expression being validated (often, it is the name of the parameter being checked).</param>
        /// <exception cref="ArgumentNullException">If the value is null.</exception>
        /// <exception cref="ArgumentException">If the value is an empty string or a string containing whitespaces only;
        /// the message describes which of these two applies.</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void NotNullOrWhitespace(string value, [CallerArgumentExpression("value")] string validatedExpression = FallbackValidatedExpression)
        {
            NotNullOrEmpty(value, validatedExpression);

            if (String.IsNullOrWhiteSpace(value))
            {
                ThrowArgumentException(validatedExpression, " may not be whitespace only.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
#if NET6_0_OR_GREATER
        [System.Diagnostics.StackTraceHidden]
#endif
        private static void ThrowArgumentNullException(string validatedExpression)
        {
            throw new ArgumentNullException(validatedExpression ?? Validate.FallbackValidatedExpression);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
#if NET6_0_OR_GREATER
        [System.Diagnostics.StackTraceHidden]
#endif
        private static void ThrowArgumentException(string validatedExpression, string additionalInfo)
        {
            if (validatedExpression == null)
            {
                validatedExpression = Validate.FallbackValidatedExpression;
            }
            else if (validatedExpression.StartsWith("\"") && validatedExpression.EndsWith("\""))
            {
                validatedExpression = "Literal expression (" + validatedExpression + ")";
            }
            else
            {
                validatedExpression = '\"' + validatedExpression + '\"';
            }

            throw new ArgumentException(validatedExpression + (additionalInfo ?? String.Empty));
        }
    }
}

#if !NETCOREAPP3_0_OR_GREATER
// When building against older frameworks, the `CallerArgumentExpressionAttribute` is missing.
// For those cases, we define it here.
// Since the attribute is only used at compile time, this will not create any issues.
// If an old compiler is used, this will be simply ignored.
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public string ParameterName { get; set; }

        public CallerArgumentExpressionAttribute(string parameterName)
        {
            ParameterName = parameterName;
        }
    }
}
#endif