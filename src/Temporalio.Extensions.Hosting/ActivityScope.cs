using System.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Temporalio.Extensions.Hosting
{
    /// <summary>
    /// Information and ability to control the activity DI scope.
    /// </summary>
    public static class ActivityScope
    {
        private static readonly AsyncLocal<IServiceScope?> ServiceScopeLocal = new();
        private static readonly AsyncLocal<object?> ScopedInstanceLocal = new();

        /// <summary>
        /// Gets or sets the current scope for this activity.
        /// </summary>
        /// <remarks>
        /// This is backed by an async local. By default, when the activity invocation starts
        /// (meaning inside the interceptor, not before), a new service scope is created and set on
        /// this value. This means it will not be present in the primary execute-activity
        /// interceptor
        /// (<see cref="Worker.Interceptors.ActivityInboundInterceptor.ExecuteActivityAsync"/>) call
        /// but will be available everywhere else the ActivityExecutionContext is. When set by the
        /// internal code, it is also disposed by the internal code. See the next remark for how to
        /// control the scope.
        /// </remarks>
        /// <remarks>
        /// In situations where a user wants to control the service scope from the primary
        /// execute-activity interceptor, this can be set to the result of <c>CreateScope</c> or
        /// <c>CreateAsyncScope</c> of a service provider. The internal code will then use this
        /// instead of creating its own, and will therefore not dispose it. This should never be set
        /// anywhere but inside the primary execute-activity interceptor, and it no matter the value
        /// it will be set to null before the <c>base</c> call returns from the primary
        /// execute-activity interceptor.
        /// </remarks>
        public static IServiceScope? ServiceScope
        {
            get => ServiceScopeLocal.Value;
            set => ServiceScopeLocal.Value = value;
        }

        /// <summary>
        /// Gets or sets the scoped instance for non-static activity methods.
        /// </summary>
        /// <remarks>
        /// This is backed by an async local. By default, when the activity invocation starts
        /// (meaning inside the interceptor, not before) for a non-static method, an instance is
        /// obtained from the service provider and set on this value. This means it will not be
        /// present in the primary execute-activity interceptor
        /// (<see cref="Worker.Interceptors.ActivityInboundInterceptor.ExecuteActivityAsync"/>) call
        /// but will be available everywhere else the ActivityExecutionContext is. See the next
        /// remark for how to control the instance.
        /// </remarks>
        /// <remarks>
        /// In situations where a user wants to control the instance from the primary
        /// execute-activity interceptor, this can be set to the result of <c>GetRequiredService</c>
        /// of a service provider. The internal code will then use this instead of creating its own.
        /// This should never be set anywhere but inside the primary execute-activity interceptor,
        /// and it no matter the value it will be set to null before the <c>base</c> call returns
        /// from the primary execute-activity interceptor.
        /// </remarks>
        public static object? ScopedInstance
        {
            get => ScopedInstanceLocal.Value;
            set => ScopedInstanceLocal.Value = value;
        }
    }
}