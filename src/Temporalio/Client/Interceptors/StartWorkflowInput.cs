
using System;
using System.Collections.Generic;
using Temporalio.Api.Common.V1;

namespace Temporalio.Client.Interceptors
{
    public record StartWorkflowInput(
        string Workflow,
        IReadOnlyCollection<object?> Args,
        StartWorkflowOptions Options,
        IDictionary<string, Payload>? Headers
    );
}