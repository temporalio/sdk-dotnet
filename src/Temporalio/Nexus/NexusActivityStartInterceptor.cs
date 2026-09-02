using System.Threading.Tasks;
using NexusRpc.Handlers;
using Temporalio.Client;
using Temporalio.Client.Interceptors;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Gives a raw activity start issued from inside a Nexus start handler the same request ID
    /// and inbound links a guarded start already gets.
    /// </summary>
    internal sealed class NexusActivityStartInterceptor : ClientOutboundInterceptor
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NexusActivityStartInterceptor"/> class.
        /// </summary>
        /// <param name="next">Next interceptor in the chain.</param>
        internal NexusActivityStartInterceptor(ClientOutboundInterceptor next)
            : base(next)
        {
        }

        /// <inheritdoc />
        public override Task<ActivityHandle<TResult>> StartActivityAsync<TResult>(
            StartActivityInput input)
        {
            if (NexusOperationExecutionContext.HasCurrent &&
                input.Options.RequestId == null &&
                NexusOperationExecutionContext.Current.HandlerContext is
                    OperationStartContext nexusStartContext)
            {
                var nexusExecutionContext = NexusOperationExecutionContext.Current;
                var options = (StartActivityOptions)input.Options.Clone();
                var links = NexusOperationStartHelper.CreateInboundLinks(
                    nexusStartContext, nexusExecutionContext);
                if (links != null)
                {
                    options.Links = links;
                }
                // The server rejects AttachRequestId without an accompanying link or
                // completion callback to attach, so only set this when there are links.
                if (links != null &&
                    options.IdConflictPolicy == Api.Enums.V1.ActivityIdConflictPolicy.UseExisting)
                {
                    options.OnConflictOptions = new()
                    {
                        AttachLinks = true,
                        AttachRequestId = true,
                    };
                }
                options.RequestId = nexusStartContext.RequestId;
                input = input with { Options = options };
            }
            return base.StartActivityAsync<TResult>(input);
        }
    }
}
