using System;
using System.Collections.Generic;
using System.Threading;

namespace Temporalio.Client
{
    public class RpcOptions
    {
        public RpcOptions(
            bool? retry = null,
            IEnumerable<KeyValuePair<string, string>>? metadata = null,
            TimeSpan? timeout = null,
            CancellationToken? cancellationToken = null
        )
        {
            Retry = retry;
            Metadata = metadata;
            Timeout = timeout;
            CancellationToken = cancellationToken;
        }

        public bool? Retry { get; set; }

        public IEnumerable<KeyValuePair<string, string>>? Metadata { get; set; }

        public TimeSpan? Timeout { get; set; }

        public CancellationToken? CancellationToken { get; set; }
    }
}
