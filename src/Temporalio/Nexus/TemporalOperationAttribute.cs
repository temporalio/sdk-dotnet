using System;

namespace Temporalio.Nexus
{
    /// <summary>
    /// Marks a method on a class with a
    /// <see cref="NexusRpc.Handlers.NexusServiceHandlerAttribute"/> as a Temporal-backed Nexus
    /// operation start handler. The method is invoked directly on every operation start rather
    /// than acting as an operation handler factory.
    /// </summary>
    /// <remarks>
    /// <para>WARNING: Nexus support is experimental.</para>
    /// <para>The method must be an instance method with signature
    /// <c>Task&lt;TemporalOperationResult&lt;TResult&gt;&gt; Method(TemporalOperationStartContext ctx,
    /// ITemporalNexusClient client[, TInput input])</c>. The third input parameter is omitted for
    /// operations with no input (i.e., <see cref="NexusRpc.Handlers.NoValue"/> input). Cancel calls
    /// are handled by the built-in
    /// <see cref="TemporalOperationHandler{TInput, TResult}"/> logic (typically canceling the
    /// underlying workflow).</para>
    /// <para>This attribute is mutually exclusive with
    /// <see cref="NexusRpc.Handlers.NexusOperationHandlerAttribute"/> on the same method. The
    /// operation this method handles is matched by C# method name to the corresponding
    /// <see cref="NexusRpc.NexusOperationAttribute"/> method on the service interface.</para>
    /// <para>Example:</para>
    /// <code>
    /// [NexusServiceHandler(typeof(ITransferService))]
    /// public class TransferServiceImpl
    /// {
    ///     [TemporalOperation]
    ///     public Task&lt;TemporalOperationResult&lt;TransferResult&gt;&gt; DoSomething(
    ///         TemporalOperationStartContext ctx, ITemporalNexusClient client, TransferInput input) =&gt;
    ///         client.StartWorkflowAsync(
    ///             (TransferWorkflow wf) =&gt; wf.RunAsync(input),
    ///             new() { Id = $"transfer-{input.TransferId}" });
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public sealed class TemporalOperationAttribute : Attribute
    {
    }
}
