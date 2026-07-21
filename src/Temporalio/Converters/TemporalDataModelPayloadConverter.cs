using System;
using System.Collections.Concurrent;
using System.Reflection;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Payload converter wrapper that applies Temporal data-model hooks.
    /// </summary>
    internal sealed class TemporalDataModelPayloadConverter :
        IPayloadConverter,
        IWithSerializationContext<IPayloadConverter>
    {
        private static readonly ConcurrentDictionary<Type, ITemporalDataModelConverter?> Converters = new();
        private readonly IPayloadConverter inner;

        private TemporalDataModelPayloadConverter(IPayloadConverter inner) => this.inner = inner;

        /// <summary>
        /// Wrap a payload converter unless it is already wrapped.
        /// </summary>
        /// <param name="payloadConverter">Payload converter to wrap.</param>
        /// <returns>Wrapped payload converter.</returns>
        public static IPayloadConverter Wrap(IPayloadConverter payloadConverter) =>
            payloadConverter is TemporalDataModelPayloadConverter ?
                payloadConverter : new TemporalDataModelPayloadConverter(payloadConverter);

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            var converter = value == null ? null : Converters.GetOrAdd(value.GetType(), CreateConverter);
            if (converter != null)
            {
                value = converter.ToDataModel(value, inner);
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

            var dataModelValue = inner.ToValue(payload, converter.DataModelType);
            return converter.FromDataModel(dataModelValue, inner);
        }

        /// <inheritdoc/>
        public IPayloadConverter WithSerializationContext(ISerializationContext context)
        {
            if (inner is not IWithSerializationContext<IPayloadConverter> withContext)
            {
                return this;
            }

            var contextInner = withContext.WithSerializationContext(context);
            return ReferenceEquals(contextInner, inner) ? this : new TemporalDataModelPayloadConverter(contextInner);
        }

        private static ITemporalDataModelConverter? CreateConverter(Type type)
        {
            var attr = type.GetCustomAttribute<TemporalDataModelAttribute>(inherit: true);
            if (attr == null)
            {
                return null;
            }

            if (!typeof(ITemporalDataModelConverter).IsAssignableFrom(attr.ConverterType))
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal data-model converter type " +
                    $"{attr.ConverterType} that does not implement {nameof(ITemporalDataModelConverter)}.");
            }
            if (attr.ConverterType.ContainsGenericParameters)
            {
                throw new InvalidOperationException(
                    $"Type {type} has an open generic Temporal data-model converter type " +
                    $"{attr.ConverterType}.");
            }

            if (Activator.CreateInstance(attr.ConverterType) is not ITemporalDataModelConverter converter)
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal data-model converter type " +
                    $"{attr.ConverterType} that could not be instantiated.");
            }
            if (converter.DataModelType == null)
            {
                throw new InvalidOperationException(
                    $"Type {type} has a Temporal data-model converter type " +
                    $"{attr.ConverterType} with a null data-model type.");
            }

            return converter;
        }
    }
}
