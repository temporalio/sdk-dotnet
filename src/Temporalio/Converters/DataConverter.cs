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
    {
        /// <summary>
        /// Gets default data converter instance.
        /// </summary>
        public static DataConverter Default { get; } =
            new(new DefaultPayloadConverter(), new DefaultFailureConverter());
    }
}
