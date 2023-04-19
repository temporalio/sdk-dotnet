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
    public record DataConverter
    {
        private readonly Lazy<IPayloadConverter> lazyPayloadConverter;
        private readonly Lazy<IFailureConverter> lazyFailureConverter;

        /// <summary>
        /// Initializes a new instance of the <see cref="DataConverter"/> class.
        /// </summary>
        /// <param name="payloadConverterType">Payload converter type. This must be a type and not
        /// an instance so it can be serialized across a sandbox boundary.</param>
        /// <param name="failureConverterType">Failure converter type. This must be a type and not
        /// an instance so it can be serialized across a sandbox boundary.</param>
        /// <param name="payloadCodec">Optional payload codec.</param>
        public DataConverter(
            Type payloadConverterType,
            Type failureConverterType,
            IPayloadCodec? payloadCodec = null)
        {
            PayloadConverterType = payloadConverterType;
            FailureConverterType = failureConverterType;
            PayloadCodec = payloadCodec;
            lazyPayloadConverter = new(() =>
                Activator.CreateInstance(PayloadConverterType) as IPayloadConverter
                    ?? throw new ArgumentException("Payload converter type not an IPayloadConverter"));
            lazyFailureConverter = new(() =>
                Activator.CreateInstance(FailureConverterType) as IFailureConverter
                    ?? throw new ArgumentException("Failure converter type not an IFailureConverter"));
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DataConverter"/> class. Copy constructor.
        /// </summary>
        /// <param name="other">Other one to copy.</param>
        protected DataConverter(DataConverter other)
        {
            PayloadConverterType = other.PayloadConverterType;
            FailureConverterType = other.FailureConverterType;
            PayloadCodec = other.PayloadCodec;
            lazyPayloadConverter = new(() =>
                Activator.CreateInstance(PayloadConverterType) as IPayloadConverter
                    ?? throw new ArgumentException("Payload converter type not an IPayloadConverter"));
            lazyFailureConverter = new(() =>
                Activator.CreateInstance(FailureConverterType) as IFailureConverter
                    ?? throw new ArgumentException("Failure converter type not an IFailureConverter"));
        }

        /// <summary>
        /// Gets default data converter instance.
        /// </summary>
        public static DataConverter Default { get; } =
            new DataConverter<DefaultPayloadConverter, DefaultFailureConverter>();

        /// <summary>
        /// Gets the payload converter type.
        /// </summary>
        public Type PayloadConverterType { get; init; }

        /// <summary>
        /// Gets the failure converter type.
        /// </summary>
        public Type FailureConverterType { get; init; }

        /// <summary>
        /// Gets the payload codec.
        /// </summary>
        public IPayloadCodec? PayloadCodec { get; init; }

        /// <summary>
        /// Gets the eagerly instantiated instance of a payload converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public IPayloadConverter PayloadConverter => lazyPayloadConverter.Value;

        /// <summary>
        /// Gets the eagerly instantiated instance of a failure converter. Note, this may not be the
        /// exact instance used in workflows.
        /// </summary>
        public IFailureConverter FailureConverter => lazyFailureConverter.Value;
    }
}
