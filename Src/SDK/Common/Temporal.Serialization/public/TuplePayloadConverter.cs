using System;
using Temporal.Util;
using Temporal.Api.Common.V1;

namespace Temporal.Serialization
{
    /// <summary>
    /// <para>
    /// This is a sample. In the long run, we will move it to the samples directory.
    /// For now, we keep it here to exemplify custom <c>IPayloadConverter</c> implementation.
    /// </para>
    /// The "build-in" payload converters can only de-/serialize a <c>Payloads</c>-collection with a single
    /// <c>Payload</c>-entry (see namespace <c>Temporal.Api.Common.V1</c> for <c>Payload</c> and <c>Payloads</c>).
    /// An exception is the <see cref="UnnamedContainerPayloadConverter"/>, which can deal with any number of
    /// <c>Payload</c>-entries, but can only de-/serialize to and from an
    /// <see cref="Temporal.Common.Payloads.PayloadContainers.IUnnamed"/>-container.
    /// Such container offers strongly-typed lazy access to the underlying data.
    /// Notably, users may provide their own implementations of <see cref="IPayloadConverter"/> that handle
    /// mupliple <c>Payload</c>-entries. This class is an example of auch a <c>IPayloadConverter</c>.
    /// It that can convert any 3-tuple to/from a <c>Payloads</c>-collection with three <c>Payload</c>-entries.
    /// (Note that a production implementation is likely to be based on a <c>ValueTuple</c> type.)
    /// </summary>
    /// <seealso cref="Temporal.Api.Common.V1.Payloads"/>
    /// <seealso cref="Temporal.Api.Common.V1.Payload"/>
    public sealed class TuplePayloadConverter<T1, T2, T3> : DelegatingPayloadConverterBase
    {
        public override bool TrySerialize<T>(T item, Payloads serializedDataAccumulator)
        {
            if (item != null && item is Tuple<T1, T2, T3> tuple)
            {
                Validate.NotNull(serializedDataAccumulator);

                SerializeItem<T1>(tuple.Item1, serializedDataAccumulator);
                SerializeItem<T2>(tuple.Item2, serializedDataAccumulator);
                SerializeItem<T3>(tuple.Item3, serializedDataAccumulator);
            }

            return false;
        }

        public override bool TryDeserialize<T>(Payloads serializedData, out T deserializedItem)
        {
            Validate.NotNull(serializedData);

            if (SerializationUtil.GetPayloadCount(serializedData) == 3
                    && typeof(Tuple<T1, T2, T3>).IsAssignableFrom(typeof(T)))
            {
                deserializedItem = Tuple.Create(DeserializeItem<T1>(CreatePayloadWrapper(serializedData, 0)),
                                                DeserializeItem<T2>(CreatePayloadWrapper(serializedData, 1)),
                                                DeserializeItem<T3>(CreatePayloadWrapper(serializedData, 2)))
                                        .Cast<Tuple<T1, T2, T3>, T>();
                return true;
            }

            deserializedItem = default(T);
            return false;
        }

        private void SerializeItem<T>(T item, Payloads serializedDataAccumulator)
        {
            try
            {
                PayloadConverter.Serialize(DelegateConvertersContainer, item, serializedDataAccumulator);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error serializing tuple-item of type {Format.QuoteOrNull(item?.GetType().FullName)}.",
                                                    ex);
            }
        }

        private T DeserializeItem<T>(Payloads itemData)
        {
            try
            {
                return PayloadConverter.Deserialize<T>(DelegateConvertersContainer, itemData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error serializing tuple-item of type {typeof(T).FullName}.",
                                                    ex);
            }
        }


        private Payloads CreatePayloadWrapper(Payloads serializedData, int index)
        {
            Payloads wrapper = new();
            wrapper.Payloads_.Add(serializedData.Payloads_[index]);
            return wrapper;
        }

        public override bool Equals(object obj)
        {
            return Object.ReferenceEquals(this, obj) || ((obj != null) && this.GetType().Equals(obj.GetType()));
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
