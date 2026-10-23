using System;
using System.Collections.Generic;
using System.Linq;
using SteelValidation.Abstractions;

namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// Deterministic, Tekla-independent rules for the V4 read-only numbering
    /// report. The rules only describe numbering: they never propose new
    /// positions, renumber parts or repair gaps. Gap repair depends on a
    /// Fabrication Released check that is not defined yet.
    /// </summary>
    public static class NumberingRules
    {
        public const string PartPositionKind = "PART";
        public const string AssemblyPositionKind = "ASSEMBLY";

        public const string MissingPositionRule = "MISSING_POSITION";
        public const string UnparsedPositionRule = "UNPARSED_POSITION";
        public const string OutOfDatePositionRule = "OUT_OF_DATE_POSITION";
        public const string DuplicatePositionRule = "DUPLICATE_POSITION";
        public const string NumberingGapRule = "NUMBERING_GAP";
        public const string SeriesStartNotOneRule = "SERIES_START_NOT_ONE";
        public const string NumberingStatusRule = "NUMBERING_STATUS";

        public const string WarningStatus = "WARNING";
        public const string UnknownStatus = "UNKNOWN";
        public const string InfoStatus = "INFO";

        public const string UpToDateStatus = "UP_TO_DATE";
        public const string NotUpToDateStatus = "NOT_UP_TO_DATE";
        public const string UpToDateUnknownStatus = "UNKNOWN";

        /// <summary>
        /// Builds the full report. Part positions and assembly positions are
        /// evaluated separately with <see cref="CheckPositions"/>. Parts are
        /// listed by object id.
        /// </summary>
        public static NumberingReportResult BuildReport(
            IEnumerable<NumberingReportPart> parts,
            NumberingReportOptions? options = null)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            var inputParts = parts.ToList();
            if (inputParts.Any(part => part == null))
            {
                throw new ArgumentException(
                    "The part collection cannot contain null entries.",
                    nameof(parts));
            }

            var effectiveOptions = options ?? new NumberingReportOptions();

            // Deterministic order: by object id, then by input order for
            // equal ids, so the report does not depend on Tekla enumeration.
            var orderedParts = inputParts
                .Select((part, index) => new { Part = part, Index = index })
                .OrderBy(item => item.Part.ObjectId)
                .ThenBy(item => item.Index)
                .Select(item => item.Part)
                .ToList();

            var result = new NumberingReportResult
            {
                PartsRead = orderedParts.Count,
                MaxMissingNumbersPerPrefix =
                    effectiveOptions.MaxMissingNumbersPerPrefix
            };

            foreach (var part in orderedParts)
            {
                string status;
                if (!part.IsNumberingUpToDate.HasValue)
                {
                    status = UpToDateUnknownStatus;
                    result.NumberingStatusUnknownCount++;
                }
                else if (part.IsNumberingUpToDate.Value)
                {
                    status = UpToDateStatus;
                    result.NumberingUpToDateCount++;
                }
                else
                {
                    status = NotUpToDateStatus;
                    result.NumberingNotUpToDateCount++;
                }

                result.Parts.Add(new NumberingPartItem
                {
                    ObjectId = part.ObjectId,
                    PartPosition = part.PartPosition ?? string.Empty,
                    AssemblyPosition = part.AssemblyPosition ?? string.Empty,
                    IsNumberingUpToDate = part.IsNumberingUpToDate,
                    NumberingStatus = status
                });
            }

            result.PartPositions = CheckPositions(
                PartPositionKind,
                orderedParts.Select(part => new KeyValuePair<int, string?>(
                    part.ObjectId, part.PartPosition)),
                effectiveOptions,
                StartNumbers(orderedParts, part => part.PartStartNumber));

            result.AssemblyPositions = CheckPositions(
                AssemblyPositionKind,
                orderedParts.Select(part => new KeyValuePair<int, string?>(
                    part.ObjectId, part.AssemblyPosition)),
                effectiveOptions,
                StartNumbers(orderedParts, part => part.AssemblyStartNumber));

            return result;
        }

        /// <summary>
        /// Applies the missing, unparsed, duplicate, series start and gap rules
        /// to one kind of position, given as (object id, position) pairs.
        /// Findings are ordered: missing and unparsed positions by object id,
        /// then duplicate groups by position (ordinal), then series start
        /// findings by prefix (ordinal). Gaps are ordered by prefix
        /// (ordinal) and only prefixes with at least one missing number are
        /// listed.
        /// </summary>
        /// <param name="startNumbers">
        /// Tekla numbering series start number per object id, when read. A
        /// prefix starts at the lowest start number of its objects, or at 1
        /// when none is known; lower numbers are not "missing" (e.g. a ROD
        /// series that starts at 3001).
        /// </param>
        public static NumberingPositionReport CheckPositions(
            string positionKind,
            IEnumerable<KeyValuePair<int, string?>> positions,
            NumberingReportOptions? options = null,
            IReadOnlyDictionary<int, int>? startNumbers = null)
        {
            if (positions == null)
                throw new ArgumentNullException(nameof(positions));

            var effectiveOptions = options ?? new NumberingReportOptions();
            var kind = positionKind ?? string.Empty;
            var parsedPositions = positions
                .Select((entry, index) => new
                {
                    ObjectId = entry.Key,
                    Index = index,
                    Position = NumberingPosition.Parse(entry.Value)
                })
                .OrderBy(item => item.ObjectId)
                .ThenBy(item => item.Index)
                .ToList();

            var report = new NumberingPositionReport { PositionKind = kind };
            var unparsedFindings = new List<NumberingFinding>();
            var outOfDateFindings = new List<NumberingFinding>();
            var numbersByPrefix = new Dictionary<string, SortedSet<int>>(
                StringComparer.Ordinal);
            var objectIdsByPosition = new Dictionary<string, SortedSet<int>>(
                StringComparer.Ordinal);
            var startByPrefix = new Dictionary<string, int>(StringComparer.Ordinal);
            var startAboveOneByPrefix = new Dictionary<string, (SortedSet<int> Starts, SortedSet<int> ObjectIds)>(
                StringComparer.Ordinal);

            foreach (var item in parsedPositions)
            {
                var position = item.Position;

                if (position.IsMissing)
                {
                    report.MissingPositionCount++;
                    report.Findings.Add(CreateFinding(
                        MissingPositionRule,
                        WarningStatus,
                        kind,
                        string.Empty,
                        new List<int> { item.ObjectId },
                        DescribeKind(kind) + " position is empty."));
                    continue;
                }

                if (!objectIdsByPosition.TryGetValue(position.Position, out var ids))
                {
                    ids = new SortedSet<int>();
                    objectIdsByPosition.Add(position.Position, ids);
                }

                ids.Add(item.ObjectId);

                // Tekla's "(?)": the number is from an earlier run and will
                // change, so like an unparsed position it takes no part in
                // the gap rule; it is reported as what it is.
                if (position.IsOutOfDate)
                {
                    report.OutOfDatePositionCount++;
                    outOfDateFindings.Add(CreateFinding(
                        OutOfDatePositionRule,
                        WarningStatus,
                        kind,
                        position.Position,
                        new List<int> { item.ObjectId },
                        DescribeKind(kind) + " position '" + position.Position +
                        "' is marked out of date by Tekla; numbering must run again."));
                    continue;
                }

                if (!position.IsParsed)
                {
                    report.UnparsedPositionCount++;
                    unparsedFindings.Add(CreateFinding(
                        UnparsedPositionRule,
                        UnknownStatus,
                        kind,
                        position.Position,
                        new List<int> { item.ObjectId },
                        DescribeKind(kind) + " position '" + position.Position +
                        "' has no usable trailing number; prefix and number were not guessed."));
                    continue;
                }

                var prefix = position.Prefix!;
                if (!numbersByPrefix.TryGetValue(prefix, out var numbers))
                {
                    numbers = new SortedSet<int>();
                    numbersByPrefix.Add(prefix, numbers);
                }

                numbers.Add(position.Number!.Value);

                if (startNumbers != null &&
                    startNumbers.TryGetValue(item.ObjectId, out var start) && start >= 1 &&
                    (!startByPrefix.TryGetValue(prefix, out var known) || start < known))
                {
                    startByPrefix[prefix] = start;
                }

                if (startNumbers != null &&
                    startNumbers.TryGetValue(item.ObjectId, out var ownStart) && ownStart > 1)
                {
                    if (!startAboveOneByPrefix.TryGetValue(prefix, out var above))
                    {
                        above = (new SortedSet<int>(), new SortedSet<int>());
                        startAboveOneByPrefix.Add(prefix, above);
                    }

                    above.Starts.Add(ownStart);
                    above.ObjectIds.Add(item.ObjectId);
                }
            }

            report.Findings.AddRange(unparsedFindings);
            report.Findings.AddRange(outOfDateFindings);

            foreach (var entry in objectIdsByPosition
                .Where(entry => entry.Value.Count > 1)
                .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                report.DuplicatePositionGroupCount++;
                report.Findings.Add(CreateFinding(
                    DuplicatePositionRule,
                    InfoStatus,
                    kind,
                    entry.Key,
                    entry.Value.ToList(),
                    entry.Value.Count + " parts share " +
                    DescribeKind(kind).ToLowerInvariant() + " position '" +
                    entry.Key + "'. Identical parts share a position by design; information only."));
            }

            // Owner decision (Mehmet, 2026-09-30): every numbering series
            // starts at 1; starts such as 1001 or 3001 are not wanted.
            foreach (var entry in startAboveOneByPrefix
                .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                report.Findings.Add(CreateFinding(
                    SeriesStartNotOneRule,
                    WarningStatus,
                    kind,
                    entry.Key,
                    entry.Value.ObjectIds.ToList(),
                    DescribeKind(kind) + " prefix '" + entry.Key + "' numbering series starts at " +
                    string.Join(", ", entry.Value.Starts) + ". Owner rule: every series starts at 1; " +
                    "set the start number to 1 in the numbering settings, then renumber on a dated Save As copy."));
            }

            foreach (var entry in numbersByPrefix
                .OrderBy(entry => entry.Key, StringComparer.Ordinal))
            {
                var gap = FindGap(
                    kind,
                    entry.Key,
                    entry.Value,
                    startByPrefix.TryGetValue(entry.Key, out var seriesStart) ? seriesStart : 1,
                    effectiveOptions.MaxMissingNumbersPerPrefix);

                if (gap == null)
                    continue;

                report.TotalMissingNumberCount += gap.MissingCount;
                report.Gaps.Add(gap);
            }

            return report;
        }

        /// <summary>
        /// Tekla series start number per object id; objects without a known
        /// start are left out.
        /// </summary>
        private static IReadOnlyDictionary<int, int> StartNumbers(
            IEnumerable<NumberingReportPart> parts,
            Func<NumberingReportPart, int?> startNumber)
        {
            var result = new Dictionary<int, int>();
            foreach (var part in parts)
            {
                var start = startNumber(part);
                if (!start.HasValue || start.Value < 1)
                    continue;

                if (!result.TryGetValue(part.ObjectId, out var known) || start.Value < known)
                    result[part.ObjectId] = start.Value;
            }

            return result;
        }

        /// <summary>
        /// Returns the numbers missing between the series start number and the
        /// highest number in use, or null when there is no gap. The list holds
        /// at most maxListed numbers in ascending order; MissingCount is the
        /// full total. Numbers below the start (and below 1) are outside the
        /// range and ignored.
        /// </summary>
        private static NumberingGap? FindGap(
            string kind,
            string prefix,
            SortedSet<int> numbersInUse,
            int startNumber,
            int maxListed)
        {
            var first = Math.Max(1, startNumber);
            var numbersInRange = numbersInUse.Where(number => number >= first).ToList();
            if (numbersInRange.Count == 0)
                return null;

            var highest = numbersInRange[numbersInRange.Count - 1];
            var missingCount = highest - first + 1 - numbersInRange.Count;
            if (missingCount == 0)
                return null;

            var missingNumbers = new List<int>();
            var expected = first;
            foreach (var number in numbersInRange)
            {
                while (expected < number && missingNumbers.Count < maxListed)
                {
                    missingNumbers.Add(expected);
                    expected++;
                }

                if (missingNumbers.Count >= maxListed)
                    break;

                expected = number + 1;
            }

            return new NumberingGap
            {
                PositionKind = kind,
                Prefix = prefix,
                StartNumber = first,
                HighestNumber = highest,
                MissingCount = missingCount,
                MissingNumbers = missingNumbers
            };
        }

        private static NumberingFinding CreateFinding(
            string rule,
            string status,
            string kind,
            string position,
            List<int> objectIds,
            string message)
        {
            return new NumberingFinding
            {
                Rule = rule,
                Status = status,
                PositionKind = kind,
                Position = position,
                ObjectIds = objectIds,
                Message = message
            };
        }

        private static string DescribeKind(string kind)
        {
            switch (kind)
            {
                case PartPositionKind: return "Part";
                case AssemblyPositionKind: return "Assembly";
                default: return kind.Length == 0 ? "The" : kind;
            }
        }
    }
}
