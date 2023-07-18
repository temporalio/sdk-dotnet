using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Temporalio.Api.WorkflowService.V1;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Description of a schedule.
    /// </summary>
    public class ScheduleDescription
    {
        private readonly Lazy<IReadOnlyDictionary<string, IEncodedRawValue>> memo;
        private readonly Lazy<SearchAttributeCollection> searchAttributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduleDescription"/> class.
        /// </summary>
        /// <param name="id">Workflow ID.</param>
        /// <param name="rawDescription">Raw proto description.</param>
        /// <param name="dataConverter">Data converter.</param>
        internal ScheduleDescription(
            string id, DescribeScheduleResponse rawDescription, DataConverter dataConverter)
        {
            Id = id;
            RawDescription = rawDescription;
            // Search attribute conversion is cheap so it doesn't need to lock on publication. But
            // memo conversion may use remote codec so it should only ever be created once lazily.
            memo = new(
                () => rawDescription.Memo == null ? new Dictionary<string, IEncodedRawValue>(0) :
                    rawDescription.Memo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IEncodedRawValue)new EncodedRawValue(dataConverter, kvp.Value)));
            searchAttributes = new(
                () => rawDescription.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawDescription.SearchAttributes),
                LazyThreadSafetyMode.PublicationOnly);
            Schedule = Schedule.FromProto(rawDescription.Schedule, dataConverter);
            Info = ScheduleInfo.FromProto(rawDescription.Info);
        }

        /// <summary>
        /// Gets the ID of the schedule.
        /// </summary>
        public string Id { get; private init; }

        /// <summary>
        /// Gets information about the schedule.
        /// </summary>
        public ScheduleInfo Info { get; private init; }

        /// <summary>
        /// Gets the schedule memo dictionary, lazily creating when accessed. The values are
        /// encoded.
        /// </summary>
        public IReadOnlyDictionary<string, IEncodedRawValue> Memo => memo.Value;

        /// <summary>
        /// Gets the schedule details. All workflow arguments on this schedule are set as
        /// <see cref="IEncodedRawValue" />.
        /// </summary>
        public Schedule Schedule { get; private init; }

        /// <summary>
        /// Gets the search attributes on the schedule.
        /// </summary>
        /// <remarks>
        /// This is lazily converted on first access.
        /// </remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the raw proto description.
        /// </summary>
        internal DescribeScheduleResponse RawDescription { get; private init; }
    }
}