using System;
using System.Collections.Generic;
using System.Linq;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Common;

namespace Temporalio.Client
{
    /// <summary>
    /// Representation of a count from a count activities call.
    /// </summary>
    /// <remarks>WARNING: Standalone activities are experimental.</remarks>
    public class ActivityExecutionCount
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ActivityExecutionCount" /> class.
        /// </summary>
        /// <param name="raw">Raw proto.</param>
        /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
        protected internal ActivityExecutionCount(CountActivityExecutionsResponse raw)
        {
            Count = raw.Count;
            Groups = raw.Groups?.Select(
                g => new AggregationGroup(g)).ToArray() ?? Array.Empty<AggregationGroup>();
        }

        /// <summary>
        /// Gets the approximate number of activities matching the original query. If the query had a
        /// group-by clause, this is simply the sum of all the counts in <see cref="Groups" />.
        /// </summary>
        public long Count { get; private init; }

        /// <summary>
        /// Gets the groups if the query had a group-by clause, or empty if not.
        /// </summary>
        public IReadOnlyCollection<AggregationGroup> Groups { get; private init; }

        /// <summary>
        /// Aggregation group if the activity count query had a group-by clause.
        /// </summary>
        /// <remarks>WARNING: Standalone activities are experimental.</remarks>
        public class AggregationGroup
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="AggregationGroup"/> class.
            /// </summary>
            /// <param name="raw">Raw proto.</param>
            /// <remarks>WARNING: This constructor may be mutated in backwards incompatible ways.</remarks>
            protected internal AggregationGroup(CountActivityExecutionsResponse.Types.AggregationGroup raw)
            {
                Count = raw.Count;
                GroupValues = raw.GroupValues?.
                    Select(SearchAttributeCollection.PayloadToObject).
                    Where(v => v != null).
                    Select(v => v!.Value.Value).
                    ToArray() ?? Array.Empty<object>();
            }

            /// <summary>
            /// Gets the approximate number of activities matching the original query for this group.
            /// </summary>
            public long Count { get; private init; }

            /// <summary>
            /// Gets the search attribute values for this group.
            /// </summary>
            public IReadOnlyCollection<object> GroupValues { get; private init; }
        }
    }
}
