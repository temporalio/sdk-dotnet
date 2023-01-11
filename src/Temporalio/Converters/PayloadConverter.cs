using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Google.Protobuf;
using Temporalio.Api.Common.V1;

namespace Temporalio.Converters
{
    /// <summary>
    /// Default implementation of a payload converter which iterates over a collection of
    /// <see cref="IEncodingConverter" />.
    /// </summary>
    /// <remarks>
    /// See <see cref="PayloadConverter()" /> for the default set of encoding converters used. To
    /// create a custom converter, a new class should extend this one.
    /// </remarks>
    public class PayloadConverter : IPayloadConverter
    {
        // KeyedCollection not worth it and OrderedDictionary not generic. So we'll expose the
        // collection and maintain an internal dictionary.

        /// <summary>
        /// Encoding converters tried, in order, when converting to payload.
        /// </summary>
        public IReadOnlyCollection<IEncodingConverter> EncodingConverters { get; private init; }

        /// <summary>
        /// Encoding converters by encoding looked up when converting from payload.
        /// </summary>
        protected IReadOnlyDictionary<ByteString, IEncodingConverter> IndexedEncodingConverters
        {
            get;
            private init;
        }

        /// <summary>
        /// Create a default payload converter.
        /// </summary>
        /// <remarks>
        /// The default payload converter uses the following set of payload converters in order:
        /// <list type="bullet">
        /// <item><term><see cref="BinaryNullConverter" /></term></item>
        /// <item><term><see cref="BinaryPlainConverter" /></term></item>
        /// <item><term><see cref="JsonProtoConverter" /></term></item>
        /// <item><term><see cref="BinaryProtoConverter" /></term></item>
        /// <item><term><see cref="JsonPlainConverter" /></term></item>
        /// </list>
        /// <para>
        /// Each of those converters are tried in that order when converting to payload, stopping at
        /// the first one that can convert. This means nulls, byte arrays, and protobuf messages are
        /// all have their own encoding and anything else falls through to the JSON converter. The
        /// JSON converter will fail on anything it can't convert.
        /// </para>
        /// <para>
        /// This also means binary proto converter will never be used when converting to a payload
        /// since the JSON proto converter will accept proto objects first. It is present however
        /// for converting from payloads which may have its encoding (e.g. from another language
        /// that is using binary proto instead of JSON proto).
        /// </para>
        /// </remarks>
        public PayloadConverter() : this(new JsonSerializerOptions()) { }

        /// <summary>
        /// <see cref="PayloadConverter()" />
        /// </summary>
        /// <param name="jsonSerializerOptions">Custom serializer options.</param>
        /// <remarks>
        /// This is protected because payload converters are referenced as class types, not
        /// instances, so only subclasses would call this.
        /// </remarks>
        protected PayloadConverter(JsonSerializerOptions jsonSerializerOptions)
            : this(
                new BinaryNullConverter(),
                new BinaryPlainConverter(),
                new JsonProtoConverter(),
                new BinaryProtoConverter(),
                new JsonPlainConverter(jsonSerializerOptions)
            ) { }

        /// <summary>
        /// <see cref="PayloadConverter(IEnumerable&lt;IEncodingConverter&gt;)" />
        /// </summary>
        protected PayloadConverter(params IEncodingConverter[] encodingConverters)
            : this((IEnumerable<IEncodingConverter>)encodingConverters) { }

        /// <summary>
        /// Create a payload converter for the given encoding converters in order.
        /// </summary>
        /// <param name="encodingConverters">
        /// Encoding converters to use. Duplicate encodings not allowed.
        /// </param>
        /// <remarks>
        /// This is protected because payload converters are referenced as class types, not
        /// instances, so only subclasses would call this.
        /// </remarks>
        /// <seealso cref="PayloadConverter()" />
        protected PayloadConverter(IEnumerable<IEncodingConverter> encodingConverters)
        {
            EncodingConverters = encodingConverters.ToList().AsReadOnly();
            IndexedEncodingConverters = encodingConverters.ToDictionary(
                x => ByteString.CopyFromUtf8(x.Encoding)
            );
        }

        /// <inheritdoc />
        public Payload ToPayload(object? value)
        {
            foreach (var enc in EncodingConverters)
            {
                if (enc.TryToPayload(value, out var payload))
                {
                    return payload!;
                }
            }
            var vType = value == null ? "<null>" : value.GetType().ToString();
            throw new ArgumentException($"Value of type {vType} has no known converter");
        }

        /// <inheritdoc />
        public object? ToValue(Payload payload, Type type)
        {
            var encoding = payload.Metadata["encoding"];
            if (IndexedEncodingConverters.TryGetValue(encoding, out var converter))
            {
                return converter.ToValue(payload, type);
            }
            throw new ArgumentException($"Unknown payload encoding {encoding.ToStringUtf8()}");
        }
    }
}
