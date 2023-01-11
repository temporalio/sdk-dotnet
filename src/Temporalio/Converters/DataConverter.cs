using System;

namespace Temporalio.Converters
{
    /// <inheritdoc />
    public record DataConverter<PayloadConverterType, FailureConverterType>(
        IPayloadCodec? PayloadCodec = null
    ) : DataConverter(typeof(PayloadConverterType), typeof(FailureConverterType), PayloadCodec)
        where PayloadConverterType : IPayloadConverter, new()
        where FailureConverterType : IFailureConverter, new()
    {
        /// <inheritdoc />
        public new PayloadConverterType PayloadConverter =>
            (PayloadConverterType)base.PayloadConverter;

        /// <inheritdoc />
        public new FailureConverterType FailureConverter =>
            (FailureConverterType)base.FailureConverter;
    }

    /// <inheritdoc />
    public record DataConverter<PayloadConverterType>(IPayloadCodec? PayloadCodec = null)
        : DataConverter<PayloadConverterType, FailureConverter>(PayloadCodec)
        where PayloadConverterType : IPayloadConverter, new() { }

    /// <summary>
    /// Data converter which combines a payload converter, a failure converter, and a payload codec.
    /// </summary>
    /// <param name="PayloadConverterType">
    /// A <see cref="IPayloadConverter" /> type. This must be a type and not an instance so it can
    /// be serialized across a sandbox boundary.
    /// </param>
    /// <param name="FailureConverterType">
    /// A <see cref="IFailureConverter" /> type. This must be a type and not an instance so it can
    /// be serialized across a sandbox boundary.
    /// </param>
    /// <param name="PayloadCodec">Optional codec.</param>
    public record DataConverter(
        Type PayloadConverterType,
        Type FailureConverterType,
        IPayloadCodec? PayloadCodec = null
    )
    {
        /// <summary>
        /// Default data converter instance.
        /// </summary>
        public static DataConverter<PayloadConverter, FailureConverter> Default { get; } = new();

        /// <summary>
        /// Eagerly instantiated instance of a payload converter. Note, this may not be the exact
        /// instance used in workflows.
        /// </summary>
        public IPayloadConverter PayloadConverter { get; } =
            Activator.CreateInstance(PayloadConverterType) as IPayloadConverter
            ?? throw new ArgumentException("Payload converter type not an IPayloadConverter");

        /// <summary>
        /// Eagerly instantiated instance of a failure converter. Note, this may not be the exact
        /// instance used in workflows.
        /// </summary>
        public IFailureConverter FailureConverter { get; } =
            Activator.CreateInstance(FailureConverterType) as IFailureConverter
            ?? throw new ArgumentException("Failure converter type not an IFailureConverter");

        /// <summary>
        /// Create a default data converter with default payload and failure converters.
        /// </summary>
        /// <remarks>
        /// Non-inheriting users should use <see cref="Default" /> instead.
        /// </remarks>
        public DataConverter() : this(typeof(PayloadConverter), typeof(FailureConverter)) { }
    }
}
