namespace Temporalio.Converters
{
    /// <summary>
    /// Interface implemented by converters and codecs that want to be able to create
    /// context-specific implementations of themselves for certain workflows or activities.
    /// </summary>
    /// <remarks>
    /// The only supported implementations by the SDK are <see cref="DataConverter"/> implementing
    /// <see cref="IWithSerializationContext{T}"/> of <c>DataConverter</c>,
    /// <see cref="IPayloadConverter"/> implementing
    /// <see cref="IWithSerializationContext{T}"/> of <c>IPayloadConverter</c>,
    /// <see cref="IEncodingConverter"/> implementing
    /// <see cref="IWithSerializationContext{T}"/> of <c>IEncodingConverter</c>,
    /// <see cref="IFailureConverter"/> implementing
    /// <see cref="IWithSerializationContext{T}"/> of <c>IFailureConverter</c>, and
    /// <see cref="IPayloadCodec"/> implementing
    /// <see cref="IWithSerializationContext{T}"/> of <c>IPayloadCodec</c>. See
    /// <see cref="WithSerializationContext(ISerializationContext)"/> for details on when this is
    /// called.
    /// </remarks>
    /// <typeparam name="T">The converter or codec type that will be returned.</typeparam>
    public interface IWithSerializationContext<T>
    {
        /// <summary>
        /// Create a new instance of this converter or codec with the given serialization context.
        /// </summary>
        /// <remarks>
        /// This will be called with an instance of <see cref="ISerializationContext.Activity"/>
        /// on converters and codecs when an activity is invoked from workflow, when an activity
        /// attempt is run on an activity worker, or manually via
        /// <see cref="Client.AsyncActivityHandle.WithSerializationContext(ISerializationContext)"/>
        /// when using async activity completion.
        /// </remarks>
        /// <remarks>
        /// This will be called with an instance of <see cref="ISerializationContext.Workflow"/>
        /// on converters and codecs when a workflow is used from a client, a schedule is created
        /// or described with a workflow, a child or external workflow is used from another
        /// workflow, or when the workflow task is run in the workflow worker.
        /// </remarks>
        /// <remarks>
        /// Note, this may be called many times for the same activity/workflow, sometimes in
        /// succession when using. Do not expect a specific instance returned converter/codec to be
        /// the one always used in those workflow/activity cases, this may be called again to
        /// re-obtain the converter/codec. Also make sure this is very inexpensive to call as it
        /// may be called frequently. Users can return <c>this</c> to effectively bypass
        /// context-specific converter/codec.
        /// </remarks>
        /// <param name="context">Context to use to return a context-specific instance of a
        /// converter or codec.</param>
        /// <returns>Context-specific instance of a converter/codec to use. This may return
        /// <c>this</c> to effectively bypass the context-specific nature.</returns>
        T WithSerializationContext(ISerializationContext context);
    }
}