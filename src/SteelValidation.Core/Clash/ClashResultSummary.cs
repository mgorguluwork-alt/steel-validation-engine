using SteelValidation.Abstractions;
using System.Collections.Generic;

namespace SteelValidation.Core.Clash
{
    public static class ClashResultSummary
    {
        /// <summary>
        /// Creates a separately summarized result for an already-filtered
        /// list. The source result and its existing summary are unchanged.
        /// </summary>
        public static ClashCheckResult SummarizeFiltered(
            ClashCheckResult? source,
            IEnumerable<ClashCheckItem>? filteredItems)
        {
            var filteredResult = new ClashCheckResult
            {
                ScannedParts = source == null ? 0 : source.ScannedParts,
                NativeClashCount = source == null
                    ? -1
                    : source.NativeClashCount
            };

            if (filteredItems != null)
            {
                foreach (ClashCheckItem item in filteredItems)
                    filteredResult.Items.Add(item);
            }

            Summarize(filteredResult);
            return filteredResult;
        }

        /// <summary>
        /// Sets TotalClashes and the HIGH/MEDIUM/LOW counts from Items. Any
        /// severity other than HIGH or MEDIUM (including null) counts as LOW.
        /// Existing counts are replaced, so calling it twice is safe.
        /// </summary>
        public static void Summarize(ClashCheckResult result)
        {
            result.TotalClashes = result.Items.Count;
            result.HighCount = 0;
            result.MediumCount = 0;
            result.LowCount = 0;

            foreach (ClashCheckItem item in result.Items)
            {
                if (item.Severity == ClashRules.High)
                    result.HighCount++;
                else if (item.Severity == ClashRules.Medium)
                    result.MediumCount++;
                else
                    result.LowCount++;
            }
        }
    }
}
