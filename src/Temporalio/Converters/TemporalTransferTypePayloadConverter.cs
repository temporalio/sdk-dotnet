using System;
using System.Collections.Concurrent;
using System.Reflection;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Payload converter wrapper that applies Temporal transfer type hooks.
    /// </summary>
    internal sealed class TemporalTransferTypePayloadConverter :
        IPayloadConverter,
        IWithSerializationContext<IPayloadConverter>
    {
        private static readonly ConcurrentDictionary<Type, ITemporalTransferTypeConverter?> Converters = new();
        private readonly IPayloadConverter inner;

        private TemporalTransferTypePayloadConverter(IPayloadConverter inner) => this.inner = inner;

        /// <summary>
        /// Wrap a payload converter unless it is already wrapped.
        /// </summary>
        /// <param name="payloadConverter">Payload converter to wrap.</param>
        /// <returns>Wrapped payload converter.</returns>
        public static IPayloadConverter Wrap(IPayloadConverter payloadConverter) =>
            payloadConverter is TemporalTransferTypePayloadConverter ?
                payloadConverter : new TemporalTransferTypePayloadConverter(payloadConverter);

        /// <summary>
        /// Wrap the data converter's payload converter unless it is already wrapped.
        /// </summary>
        /// <param name="dataConverter">Data converter to wrap.</param>
        /// <returns>Data converter with a wrapped payload converter.</returns>
        public static DataConverter Wrap(DataConverter dataConverter)
        {
            var payloadConverter = Wrap(dataConverter.PayloadConverter);
            return ReferenceEquals(payloadConverter, dataConverter.PayloadConverter) ?
                dataConverter : dataConverter with { PayloadConverter = payloadConverter };
        }

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            var converter = value == null ? null : Converters.GetOrAdd(value.GetType(), CreateConverter);
            if (converter != null)
            {
                value = converter.ToTransferType(value);
            }
            return inner.ToPayload(value);
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            var converter = Converters.GetOrAdd(type, CreateConverter);
            if (converter == null)
            {
                return inner.ToValue(payload, type);
            }

            var transferTypeValue = inner.ToValue(payload, converter.TransferType);
            return converter.FromTransferType(transferTypeValue);
        }

        /// <inheritdoc/>
        public IPayloadConverter WithSerializationContext(ISerializationContext context)
        {
            if (inner is not IWithSerializationContext<IPayloadConverter> withContext)
            {
                return this;
            }

            var contextInner = withContext.WithSerializationContext(context);
            return ReferenceEquals(contextInner, inner) ? this : new TemporalTransferTypePayloadConverter(contextInner);
        }

        private static ITemporalTransferTypeConverter? CreateConverter(Type type)
        {
            var attr = type.GetCustomAttribute<TemporalTransferTypeConverterAttribute>(
                inherit: false);
            if (attr == null)
            {
                return null;
            }

            if (!typeof(ITemporalTransferTypeConverter).IsAssignableFrom(attr.ConverterType))
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal transfer type converter type " +
                    $"{attr.ConverterType} that does not implement {nameof(ITemporalTransferTypeConverter)}.");
            }
            if (attr.ConverterType.IsAbstract)
            {
                throw new InvalidOperationException(
                    $"Type {type} has an abstract Temporal transfer type converter type " +
                    $"{attr.ConverterType}.");
            }
            if (attr.ConverterType.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"Type {type} has an open generic Temporal transfer type converter type " +
                    $"{attr.ConverterType}.");
            }
            if (!attr.ConverterType.IsValueType &&
                attr.ConverterType.GetConstructor(Type.EmptyTypes) == null)
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal transfer type converter type " +
                    $"{attr.ConverterType} without a public parameterless constructor.");
            }

            if (Activator.CreateInstance(attr.ConverterType) is not ITemporalTransferTypeConverter converter)
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal transfer type converter type " +
                    $"{attr.ConverterType} that could not be instantiated.");
            }
            if (converter.TransferType == null)
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal transfer type converter type " +
                    $"{attr.ConverterType} with a null transfer type.");
            }

            return converter;
        }
    }
}
