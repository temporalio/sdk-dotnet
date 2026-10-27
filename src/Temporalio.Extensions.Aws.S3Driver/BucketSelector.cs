using Temporalio.Api.Common.V1;
using Temporalio.Converters;

namespace Temporalio.Extensions.Aws.S3Driver
{
    /// <summary>
    /// Chooses the S3 bucket a payload is stored in.
    /// </summary>
    /// <param name="context">Context the payload is being stored under.</param>
    /// <param name="payload">The payload being stored. Do not mutate this.</param>
    /// <returns>The bucket name.</returns>
    /// <remarks>
    /// WARNING: This API is experimental and may change in the future.
    /// </remarks>
    internal delegate string BucketSelector(StorageDriverStoreContext context, Payload payload);
}
