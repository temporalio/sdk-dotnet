namespace Temporalio.Extensions.Aws.SecretsManager
{
    /// <summary>
    /// A secret ID and its secret_type metric label.
    /// </summary>
    internal sealed class SecretRef
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SecretRef"/> class.
        /// </summary>
        /// <param name="id">Secret ID.</param>
        /// <param name="secretType">secret_type metric label.</param>
        public SecretRef(string id, string secretType)
        {
            Id = id;
            SecretType = secretType;
        }

        /// <summary>
        /// Gets the secret ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets the secret_type metric label.
        /// </summary>
        public string SecretType { get; }
    }
}
