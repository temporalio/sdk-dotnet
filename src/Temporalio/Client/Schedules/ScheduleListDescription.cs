using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Temporalio.Common;
using Temporalio.Converters;

namespace Temporalio.Client.Schedules
{
    /// <summary>
    /// Description of a listed schedule.
    /// </summary>
    public class ScheduleListDescription
    {
        private readonly Lazy<IReadOnlyDictionary<string, IEncodedRawValue>> memo;
        private readonly Lazy<SearchAttributeCollection> searchAttributes;

        /// <summary>
        /// Initializes a new instance of the <see cref="ScheduleListDescription"/> class.
        /// </summary>
        /// <param name="rawEntry">Raw proto.</param>
        /// <param name="dataConverter">Data converter.</param>
        internal ScheduleListDescription(
            Api.Schedule.V1.ScheduleListEntry rawEntry, DataConverter dataConverter)
        {
            RawEntry = rawEntry;
            // Search attribute conversion is cheap so it doesn't need to lock on publication. But
            // memo conversion may use remote codec so it should only ever be created once lazily.
            memo = new(
                () => rawEntry.Memo == null ? new Dictionary<string, IEncodedRawValue>(0) :
                    rawEntry.Memo.Fields.ToDictionary(
                        kvp => kvp.Key,
                        kvp => (IEncodedRawValue)new EncodedRawValue(dataConverter, kvp.Value)));
            searchAttributes = new(
                () => rawEntry.SearchAttributes == null ?
                    SearchAttributeCollection.Empty :
                    SearchAttributeCollection.FromProto(rawEntry.SearchAttributes),
                LazyThreadSafetyMode.PublicationOnly);
            if (rawEntry.Info != null)
            {
                Schedule = ScheduleListSchedule.FromProto(rawEntry.Info);
                Info = ScheduleListInfo.FromProto(rawEntry.Info);
            }
        }

        /// <summary>
        /// Gets the schedule ID.
        /// </summary>
        public string ID => RawEntry.ScheduleId;

        /// <summary>
        /// Gets the schedule.
        /// </summary>
        /// <remarks>
        /// This may not be present in older Temporal servers without advanced visibility.
        /// </remarks>
        public ScheduleListSchedule? Schedule { get; private init; }

        /// <summary>
        /// Gets information about the schedule.
        /// </summary>
        /// <remarks>
        /// This may not be present in older Temporal servers without advanced visibility.
        /// </remarks>
        public ScheduleListInfo? Info { get; private init; }

        /// <summary>
        /// Gets the schedule memo dictionary, lazily creating when accessed.
        /// </summary>
        public IReadOnlyDictionary<string, IEncodedRawValue> Memo => memo.Value;

        /// <summary>
        /// Gets the search attributes on the schedule.
        /// </summary>
        /// <remarks>
        /// This is lazily converted on first access.
        /// </remarks>
        public SearchAttributeCollection TypedSearchAttributes => searchAttributes.Value;

        /// <summary>
        /// Gets the raw proto.
        /// </summary>
        internal Api.Schedule.V1.ScheduleListEntry RawEntry { get; private init; }
    }
}