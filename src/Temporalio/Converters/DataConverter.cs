namespace Temporalio.Converters
{
    /// <summary>
    /// Data converter which combines a payload converter, a failure converter, and a payload codec.
    /// </summary>
    /// <param name="PayloadConverter">Payload converter.</param>
    /// <param name="FailureConverter">Failure converter.</param>
    /// <param name="PayloadCodec">Optional payload codec.</param>
    public record DataConverter(
        IPayloadConverter PayloadConverter,
        IFailureConverter FailureConverter,
        IPayloadCodec? PayloadCodec = null)
    : IWithSerializationContext<DataConverter>
    {
        /// <summary>
        /// Gets default data converter instance.
        /// </summary>
        public static DataConverter Default { get; } =
            new(new DefaultPayloadConverter(), new DefaultFailureConverter());

        /// <inheritdoc/>
        public virtual DataConverter WithSerializationContext(ISerializationContext context)
        {
            var payloadConverter = PayloadConverter is IWithSerializationContext<IPayloadConverter> p ?
                p.WithSerializationContext(context) : PayloadConverter;
            var failureConverter = FailureConverter is IWithSerializationContext<IFailureConverter> f ?
                f.WithSerializationContext(context) : FailureConverter;
            var payloadCodec = PayloadCodec is IWithSerializationContext<IPayloadCodec> pc ?
                pc.WithSerializationContext(context) : PayloadCodec;

            // Only create if the objects weren't the same as before. There is no benefit to using
            // whether the interface is implemented as the check since the default payload converter
            // implements the interface but returns "this" in most cases. The benefit of preventing
            // the instantiation is probably negligible, but harmless and actually helps in
            // AsyncActivityHandle.
            if (ReferenceEquals(payloadConverter, PayloadConverter) &&
                ReferenceEquals(failureConverter, FailureConverter) &&
                ReferenceEquals(payloadCodec, PayloadCodec))
            {
                return this;
            }

            return new(payloadConverter, failureConverter, payloadCodec);
        }
    }
}
