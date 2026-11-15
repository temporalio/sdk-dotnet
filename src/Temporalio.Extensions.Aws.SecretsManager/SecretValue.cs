using System.Text;

namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A fetched secret value.
    /// </summary>
    internal sealed class SecretValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecretValue"/> class.
        /// </summary>
        /// <param name="value">Secret value.</param>
        /// <param name="versionId">Secret version ID.</param>
        public SecretValue(byte[] value, string versionId)
        {
            Value = value;
            VersionId = versionId;
        }

        /// <summary>
        /// Gets the secret value.
        /// </summary>
        public byte[] Value { get; }

        /// <summary>
        /// Gets the secret version ID.
        /// </summary>
        public string VersionId { get; }

        /// <summary>
        /// Create a secret value from a GetSecretValue response's fields, preferring
        /// SecretString.
        /// </summary>
        /// <param name="secretString">SecretString field.</param>
        /// <param name="secretBinary">SecretBinary field.</param>
        /// <param name="versionId">VersionId field.</param>
        /// <returns>The secret value.</returns>
        public static SecretValue FromResponse(string? secretString, byte[]? secretBinary, string? versionId)
        {
            byte[] value;
            if (secretString != null)
            {
                value = Encoding.UTF8.GetBytes(secretString);
            }
            else if (secretBinary != null && secretBinary.Length > 0)
            {
                value = secretBinary;
            }
            else
            {
                throw new InvalidResponseException("secret has no value");
            }
            return new(value, versionId ?? string.Empty);
        }
    }
}
