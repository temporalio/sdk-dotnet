using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Temporalio.Client.Configuration
{
    /// <summary>
    /// A data source for configuration, which can be a path to a file,
    /// the string contents of a file, or raw bytes.
    /// </summary>
    [JsonConverter(typeof(DataSource.JsonConverter))]
    public sealed class DataSource
    {
        private DataSource()
        {
        }

        /// <summary>
        /// Gets the file path for this data source, if applicable.
        /// </summary>
        public string? Path { get; private set; }

        /// <summary>
        /// Gets the raw data for this data source, if applicable.
        /// </summary>
        public byte[]? Data { get; private set; }

        /// <summary>
        /// Create a data source from a file path.
        /// </summary>
        /// <param name="path">Path to the configuration file.</param>
        /// <returns>A new data source for the specified file.</returns>
        /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
        public static DataSource FromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path cannot be null or empty", nameof(path));
            }

            return new DataSource { Path = path };
        }

        /// <summary>
        /// Create a data source from string content.
        /// </summary>
        /// <param name="content">Configuration data as a string.</param>
        /// <returns>A new data source for the specified data.</returns>
        /// <exception cref="ArgumentException">Thrown when content is null.</exception>
        public static DataSource FromString(string content)
        {
            if (content == null)
            {
                throw new ArgumentException("Content cannot be null", nameof(content));
            }

            return new DataSource { Data = Encoding.UTF8.GetBytes(content) };
        }

        /// <summary>
        /// Create a data source from raw bytes.
        /// </summary>
        /// <param name="data">Configuration data as raw bytes.</param>
        /// <returns>A new data source for the specified data.</returns>
        /// <exception cref="ArgumentException">Thrown when data is null or empty.</exception>
        public static DataSource FromBytes(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }

            return new DataSource { Data = data };
        }

        /// <summary>
        /// Create a data source using default configuration discovery.
        /// </summary>
        /// <returns>A new data source using default paths.</returns>
        public static DataSource FromDefault() => new DataSource();

        /// <summary>
        /// JSON converter for DataSource objects.
        /// </summary>
        internal class JsonConverter : JsonConverter<DataSource>
        {
            /// <inheritdoc />
            public override DataSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Null)
                {
                    return FromDefault();
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException($"Unexpected token type: {reader.TokenType}");
                }

                string? path = null;
                string? data = null;

                while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var propertyName = reader.GetString()?.ToUpperInvariant();
                        reader.Read();

                        switch (propertyName)
                        {
                            case "PATH":
                                path = reader.GetString();
                                break;
                            case "DATA":
                                data = reader.GetString();
                                break;
                        }
                    }
                }

                return !string.IsNullOrEmpty(path) ? FromPath(path!)
                     : data != null ? FromString(data)
                     : FromDefault();
            }

            /// <inheritdoc />
            public override void Write(Utf8JsonWriter writer, DataSource value, JsonSerializerOptions options)
            {
                if (value == null)
                {
                    writer.WriteNullValue();
                    return;
                }

                writer.WriteStartObject();
                if (!string.IsNullOrEmpty(value.Path))
                {
                    writer.WriteString("path", value.Path);
                }
                else if (value.Data != null)
                {
                    writer.WriteString("data", Encoding.UTF8.GetString(value.Data));
                }

                writer.WriteEndObject();
            }
        }
    }
}