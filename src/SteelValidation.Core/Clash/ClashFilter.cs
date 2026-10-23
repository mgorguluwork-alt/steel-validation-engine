using System;
using System.Collections.Generic;
using SteelValidation.Abstractions;

namespace SteelValidation.Core.Clash
{
    /// <summary>
    /// Optional, operator-facing criteria for narrowing an existing clash
    /// result. Null and empty severity/type collections mean that criterion
    /// is not applied.
    /// </summary>
    public sealed class ClashFilterCriteria
    {
        public ICollection<string>? Severities { get; set; }

        public ICollection<string>? ClashTypes { get; set; }

        public string? ObjectText { get; set; }

        public double? MinimumOverlap { get; set; }
    }

    /// <summary>
    /// Pure filtering rules for already-detected clashes. This class never
    /// invokes Tekla and never changes the supplied items or collection.
    /// </summary>
    public static class ClashFilter
    {
        /// <summary>
        /// Returns a new list containing the matching input items, in their
        /// original order. A null input produces an empty list.
        /// </summary>
        public static IList<ClashCheckItem> Apply(
            IEnumerable<ClashCheckItem>? items,
            ClashFilterCriteria? criteria)
        {
            var filtered = new List<ClashCheckItem>();

            if (items == null)
                return filtered;

            HashSet<string>? severities = CreateSet(
                criteria == null ? null : criteria.Severities);
            HashSet<string>? clashTypes = CreateSet(
                criteria == null ? null : criteria.ClashTypes);
            string? objectText = NormalizeObjectText(
                criteria == null ? null : criteria.ObjectText);
            double? minimumOverlap =
                criteria == null ? null : criteria.MinimumOverlap;

            foreach (ClashCheckItem item in items)
            {
                if (item == null)
                {
                    if (severities == null &&
                        clashTypes == null &&
                        objectText == null &&
                        !minimumOverlap.HasValue)
                    {
                        filtered.Add(item!);
                    }

                    continue;
                }

                if (severities != null && !severities.Contains(item.Severity))
                    continue;

                if (clashTypes != null && !clashTypes.Contains(item.ClashType))
                    continue;

                if (objectText != null &&
                    !ContainsObjectText(item.Object1, objectText) &&
                    !ContainsObjectText(item.Object2, objectText))
                {
                    continue;
                }

                if (minimumOverlap.HasValue &&
                    !(item.Overlap > 0 &&
                      item.Overlap >= minimumOverlap.Value))
                {
                    continue;
                }

                filtered.Add(item);
            }

            return filtered;
        }

        private static HashSet<string>? CreateSet(
            ICollection<string>? values)
        {
            if (values == null || values.Count == 0)
                return null;

            return new HashSet<string>(values, StringComparer.Ordinal);
        }

        private static string? NormalizeObjectText(string? objectText)
        {
            if (string.IsNullOrWhiteSpace(objectText))
                return null;

            return objectText!.Trim();
        }

        private static bool ContainsObjectText(
            string objectDescription,
            string objectText)
        {
            return objectDescription != null &&
                objectDescription.IndexOf(
                    objectText,
                    StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
