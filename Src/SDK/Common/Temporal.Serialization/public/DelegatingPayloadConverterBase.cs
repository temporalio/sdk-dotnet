using System;
using System.Collections.Generic;
using Temporal.Util;
using Temporal.Api.Common.V1;

namespace Temporal.Serialization
{
    /// <summary>
    /// A <c>DelegatingPayloadConverterBase</c> subclass is an <c>IPayloadConverter</c> that can process complex data types
    /// while delegating the de-/serialization of substructures to other converters.
    /// </summary>
    public abstract class DelegatingPayloadConverterBase : IPayloadConverter
    {
        private IPayloadConverter _delegateConverters = null;

        protected virtual IPayloadConverter DelegateConvertersContainer
        {
            get { return _delegateConverters; }
        }

        public abstract bool TryDeserialize<T>(Payloads serializedData, out T item);

        public abstract bool TrySerialize<T>(T item, Payloads serializedDataAccumulator);

        public void InitDelegates(params IPayloadConverter[] delegateConverters)
        {
            if (delegateConverters != null
                    && delegateConverters.Length == 1
                    && delegateConverters[0] != null
                    && delegateConverters[0] is IEnumerable<IPayloadConverter> singleConverter)
            {
                InitDelegates(singleConverter);
            }
            else
            {
                InitDelegates((IEnumerable<IPayloadConverter>) delegateConverters);
            }
        }

        public virtual void InitDelegates(IEnumerable<IPayloadConverter> delegateConverters)
        {
            Validate.NotNull(delegateConverters);

            _delegateConverters = (delegateConverters is IPayloadConverter compositeConverter)
                                        ? compositeConverter
                                        : new CompositePayloadConverter(delegateConverters);
        }

        public override bool Equals(object obj)
        {
            return (obj != null)
                        && (obj is DelegatingPayloadConverterBase delegatingPayloadConverter)
                        && Equals(delegatingPayloadConverter);
        }

        /// <summary>
        /// Determines if this payload converter can be considered equal to the specified <c>other</c> converter.
        /// Among other things, converters are compared for equality when data held in a lazily deresialized container
        /// is re-serialized. In such cases, if the serializing and the deserializing converters are equal, data does not
        /// to re round-tripped.
        /// See <see cref="Temporal.Common.Payloads.PayloadContainers.Unnamed.SerializedDataBacked" /> and 
        /// <see cref="UnnamedContainerPayloadConverter" />.
        /// </summary>    
        public virtual bool Equals(DelegatingPayloadConverterBase obj)
        {
            if (Object.ReferenceEquals(this, obj))
            {
                return true;
            }

            return (obj != null)
                        && this.GetType().Equals(obj.GetType())
                        && DelegateConvertersContainer.Equals(DelegateConvertersContainer);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}
