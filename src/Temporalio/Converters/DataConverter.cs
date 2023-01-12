using System;

namespace Temporalio.Converters
{
    /// <summary>
    /// <see cref="DataConverter" /> with typed payload and failure converters.
    /// </summary>
    /// <typeparam name="TPayloadConverterType">Payload converter type.</typeparam>
    /// <typeparam name="TFailureConverterType">Failure converter type.</typeparam>
    /// <param name="PayloadCodec">Payload codec.</param>
    public record DataConverter<TPayloadConverterType, TFailureConverterType>(
        IPayloadCodec? PayloadCodec = null) : DataConverter(typeof(TPayloadConverterType), typeof(TFailureConverterType), PayloadCodec)
        where TPayloadConverterType : IPayloadConverter, new()
        where TFailureConverterType : IFailureConverter, new()
    {
        /// <summary>
        /// Gets the eagerly instantiated instance of a payload converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public new TPayloadConverterType PayloadConverter =>
            (TPayloadConverterType)base.PayloadConverter;

        /// <summary>
        /// Gets the eagerly instantiated instance of a failure converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public new TFailureConverterType FailureConverter =>
            (TFailureConverterType)base.FailureConverter;
    }

    /// <summary>
    /// <see cref="DataConverter" /> with typed payload converters.
    /// </summary>
    /// <typeparam name="TPayloadConverterType">Payload converter type.</typeparam>
    /// <param name="PayloadCodec">Payload codec.</param>
    public record DataConverter<TPayloadConverterType>(IPayloadCodec? PayloadCodec = null)
        : DataConverter<TPayloadConverterType, DefaultFailureConverter>(PayloadCodec)
        where TPayloadConverterType : IPayloadConverter, new()
    {
    }

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
        IPayloadCodec? PayloadCodec = null)
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DataConverter"/> class with default
        /// payload and failure converters.
        /// </summary>
        /// <remarks>
        /// Non-inheriting users should use <see cref="Default" /> instead.
        /// </remarks>
        public DataConverter()
            : this(typeof(DefaultPayloadConverter), typeof(DefaultFailureConverter))
        {
        }

        /// <summary>
        /// Gets default data converter instance.
        /// </summary>
        public static DataConverter<DefaultPayloadConverter, DefaultFailureConverter> Default { get; } = new();

        /// <summary>
        /// Gets the eagerly instantiated instance of a payload converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public IPayloadConverter PayloadConverter { get; } =
            Activator.CreateInstance(PayloadConverterType) as IPayloadConverter
            ?? throw new ArgumentException("Payload converter type not an IPayloadConverter");

        /// <summary>
        /// Gets the eagerly instantiated instance of a failure converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public IFailureConverter FailureConverter { get; } =
            Activator.CreateInstance(FailureConverterType) as IFailureConverter
            ?? throw new ArgumentException("Failure converter type not an IFailureConverter");
    }
}
