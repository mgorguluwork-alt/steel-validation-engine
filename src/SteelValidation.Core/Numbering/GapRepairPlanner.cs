using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// Outcome of the V4 gap decision for one position series (one position
    /// kind and prefix), following RULES NUM-002 to NUM-005.
    /// </summary>
    public enum GapRepairDecision
    {
        /// <summary>The numbers in use are already start..start+n-1.</summary>
        NoGap = 0,

        /// <summary>
        /// Every object in the series is not released and its numbering is
        /// up to date: a compaction is proposed (NUM-003). Never applied here.
        /// </summary>
        Proposed = 1,

        /// <summary>At least one object in the series is released (NUM-004).</summary>
        BlockedReleased = 2,

        /// <summary>
        /// At least one object has an unknown release state. Unknown is the
        /// safe default while no release data source is verified.
        /// </summary>
        BlockedUnknownReleaseState = 3,

        /// <summary>
        /// At least one object's numbering is not up to date or its status is
        /// unknown, so its current position cannot be trusted as a base.
        /// </summary>
        BlockedNumberingStale = 4,

        /// <summary>
        /// The input was not the whole model (for example a selection). A
        /// number that looks free may be used by a part outside the input,
        /// so a compaction could create a duplicate position.
        /// </summary>
        BlockedPartialInput = 5,

        /// <summary>
        /// This series is current, but numbering elsewhere in the model is out
        /// of date ("(?)" or not up to date). Tekla numbers parts and
        /// assemblies together, so numbering must run before any gap is judged.
        /// </summary>
        BlockedModelNumberingNotUpToDate = 6,

        /// <summary>
        /// Objects in the series have different Tekla series start numbers.
        /// A compaction onto the lowest start could move an object below its
        /// own start, so nothing is proposed until the settings agree.
        /// </summary>
        BlockedMixedStartNumbers = 7
    }

    /// <summary>
    /// One proposed old -> new position (NUM-005). Every object that carries
    /// the old position string moves together; identical parts share one
    /// position in Tekla.
    /// </summary>
    public sealed class GapRepairChange
    {
        public GapRepairChange(
            string oldPosition,
            string newPosition,
            int oldNumber,
            int newNumber,
            IReadOnlyList<int> objectIds)
        {
            OldPosition = oldPosition;
            NewPosition = newPosition;
            OldNumber = oldNumber;
            NewNumber = newNumber;
            ObjectIds = objectIds;
        }

        public string OldPosition { get; }

        public string NewPosition { get; }

        public int OldNumber { get; }

        public int NewNumber { get; }

        /// <summary>Object ids that carry <see cref="OldPosition"/>, ascending.</summary>
        public IReadOnlyList<int> ObjectIds { get; }

        public string Reason => GapRepairPlanner.Reason;
    }

    /// <summary>The gap decision for one position kind and prefix.</summary>
    public sealed class GapRepairSeries
    {
        public GapRepairSeries(
            string positionKind,
            string prefix,
            GapRepairDecision decision,
            int startNumber,
            IReadOnlyList<int> numbersInUse,
            IReadOnlyList<GapRepairChange> changes,
            IReadOnlyList<int> blockingObjectIds)
        {
            PositionKind = positionKind;
            Prefix = prefix;
            Decision = decision;
            StartNumber = startNumber;
            NumbersInUse = numbersInUse;
            Changes = changes;
            BlockingObjectIds = blockingObjectIds;
        }

        public string PositionKind { get; }

        public string Prefix { get; }

        public GapRepairDecision Decision { get; }

        /// <summary>
        /// Tekla numbering series start number (the lowest known among the
        /// series' objects), 1 when none was read.
        /// </summary>
        public int StartNumber { get; }

        /// <summary>Distinct numbers (the start number or more) in use, ascending.</summary>
        public IReadOnlyList<int> NumbersInUse { get; }

        /// <summary>Proposed changes; empty unless <see cref="Decision"/> is Proposed.</summary>
        public IReadOnlyList<GapRepairChange> Changes { get; }

        /// <summary>
        /// For a blocked series, the objects that caused the block (released,
        /// unknown release state, stale numbering, or a start number above
        /// the series start), ascending. These are the
        /// objects to investigate under NUM-004.
        /// </summary>
        public IReadOnlyList<int> BlockingObjectIds { get; }
    }

    /// <summary>
    /// A dry-run gap repair plan. Nothing here changes the model: applying a
    /// plan is a separate, explicitly operator-triggered step that must first
    /// save the model under a dated name (owner decision, 2026-09-28).
    /// </summary>
    public sealed class GapRepairPlan
    {
        public GapRepairPlan(IReadOnlyList<GapRepairSeries> series)
        {
            Series = series;
        }

        /// <summary>Only series with a gap, ordered by kind, then prefix (ordinal).</summary>
        public IReadOnlyList<GapRepairSeries> Series { get; }

        /// <summary>Always true: the planner never applies a change.</summary>
        public bool IsDryRun => true;

        public bool HasProposals => Series.Any(series => series.Decision == GapRepairDecision.Proposed);

        /// <summary>
        /// True when applying this plan would renumber: a dated Save As of the
        /// model must come first, and applying must stop if it fails.
        /// </summary>
        public bool RequiresDatedSaveAs => HasProposals;
    }

    /// <summary>
    /// Deterministic, Tekla-independent V4 gap decision. For every position
    /// series (part or assembly positions sharing one prefix) with a gap in
    /// its numbers, it decides whether a compaction may be proposed:
    /// released anywhere in the series blocks it (NUM-004), an unknown
    /// release state blocks it (safe default), stale or unknown numbering
    /// blocks it, and so does an input that is not the whole model.
    /// Otherwise the numbers in use are mapped, in order, onto the series
    /// start..start+n-1 (C1, C2, C4, C5 -> C4 to C3, C5 to C4; NUM-003) with
    /// old -> new traceability (NUM-005). The start is Tekla's numbering
    /// series StartNumber (ROD-3001 is never moved to ROD-1), or 1 when it was
    /// not read. Missing and unparsed positions and numbers below the start
    /// take no part, like in <see cref="NumberingRules"/>.
    /// </summary>
    public static class GapRepairPlanner
    {
        public const string Reason = "NUM-003 unreleased gap compaction (proposal only)";

        /// <param name="parts">The numbered steel parts to plan for.</param>
        /// <param name="releaseStates">Release state per object id.</param>
        /// <param name="isWholeModel">
        /// True only when <paramref name="parts"/> holds every numbered steel
        /// part of the model. Otherwise no compaction is proposed
        /// (<see cref="GapRepairDecision.BlockedPartialInput"/>).
        /// </param>
        public static GapRepairPlan Plan(
            IEnumerable<NumberingReportPart> parts,
            IReleaseStateSource releaseStates,
            bool isWholeModel)
        {
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));
            if (releaseStates == null)
                throw new ArgumentNullException(nameof(releaseStates));

            var inputParts = parts.ToList();
            if (inputParts.Any(part => part == null))
            {
                throw new ArgumentException(
                    "The part collection cannot contain null entries.",
                    nameof(parts));
            }

            // One object listed twice could land in two contradictory changes.
            var duplicate = inputParts.GroupBy(part => part.ObjectId).FirstOrDefault(group => group.Count() > 1);
            if (duplicate != null)
            {
                throw new ArgumentException(
                    "Object " + duplicate.Key.ToString(CultureInfo.InvariantCulture) +
                    " is listed more than once; each object must appear once.",
                    nameof(parts));
            }

            // Qwen review: Tekla numbers parts and assemblies together, so one
            // out-of-date object anywhere means numbering must run first.
            var modelNumberingCurrent = inputParts.All(part =>
                part.IsNumberingUpToDate == true &&
                !NumberingPosition.Parse(part.PartPosition).IsOutOfDate &&
                !NumberingPosition.Parse(part.AssemblyPosition).IsOutOfDate);

            var series = new List<GapRepairSeries>();
            series.AddRange(PlanKind(
                NumberingRules.PartPositionKind,
                inputParts.Select(part => (part, part.PartPosition, part.PartStartNumber)),
                releaseStates,
                isWholeModel,
                modelNumberingCurrent));
            series.AddRange(PlanKind(
                NumberingRules.AssemblyPositionKind,
                inputParts.Select(part => (part, part.AssemblyPosition, part.AssemblyStartNumber)),
                releaseStates,
                isWholeModel,
                modelNumberingCurrent));

            return new GapRepairPlan(series);
        }

        private static IEnumerable<GapRepairSeries> PlanKind(
            string positionKind,
            IEnumerable<(NumberingReportPart Part, string? Position, int? StartNumber)> entries,
            IReleaseStateSource releaseStates,
            bool isWholeModel,
            bool modelNumberingCurrent)
        {
            var byPrefix = new Dictionary<string, List<(NumberingReportPart Part, NumberingPosition Position)>>(StringComparer.Ordinal);
            var startsByPrefix = new Dictionary<string, List<(int ObjectId, int Start)>>(StringComparer.Ordinal);
            foreach (var (part, raw, start) in entries)
            {
                var position = NumberingPosition.Parse(raw);
                if (!position.IsParsed || position.Number!.Value < 1)
                    continue;

                if (!byPrefix.TryGetValue(position.Prefix!, out var list))
                {
                    list = new List<(NumberingReportPart, NumberingPosition)>();
                    byPrefix.Add(position.Prefix!, list);
                }

                list.Add((part, position));

                if (start.HasValue && start.Value >= 1)
                {
                    if (!startsByPrefix.TryGetValue(position.Prefix!, out var starts))
                    {
                        starts = new List<(int, int)>();
                        startsByPrefix.Add(position.Prefix!, starts);
                    }

                    starts.Add((part.ObjectId, start.Value));
                }
            }

            foreach (var entry in byPrefix.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                startsByPrefix.TryGetValue(entry.Key, out var knownStarts);
                var seriesStart = knownStarts != null ? knownStarts.Min(item => item.Start) : 1;
                var mixedStartObjectIds = knownStarts == null
                    ? new List<int>()
                    : knownStarts
                        .Where(item => item.Start != seriesStart)
                        .Select(item => item.ObjectId)
                        .Distinct()
                        .OrderBy(id => id)
                        .ToList();
                var members = entry.Value
                    .Where(member => member.Position.Number!.Value >= seriesStart)
                    .ToList();
                var numbers = members
                    .Select(member => member.Position.Number!.Value)
                    .Distinct()
                    .OrderBy(number => number)
                    .ToList();

                var hasGap = numbers.Where((number, index) => number != seriesStart + index).Any();
                if (!hasGap)
                    continue;

                yield return Decide(positionKind, entry.Key, seriesStart, mixedStartObjectIds, numbers, members, releaseStates, isWholeModel, modelNumberingCurrent);
            }
        }

        private static GapRepairSeries Decide(
            string positionKind,
            string prefix,
            int seriesStart,
            IReadOnlyList<int> mixedStartObjectIds,
            IReadOnlyList<int> numbers,
            List<(NumberingReportPart Part, NumberingPosition Position)> members,
            IReleaseStateSource releaseStates,
            bool isWholeModel,
            bool modelNumberingCurrent)
        {
            var objectIds = members.Select(member => member.Part.ObjectId).Distinct().OrderBy(id => id).ToList();
            var states = objectIds.ToDictionary(id => id, releaseStates.GetState);

            var released = objectIds.Where(id => states[id] == FabricationReleaseState.Released).ToList();
            if (released.Count > 0)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedReleased, seriesStart, numbers, released);

            var unknown = objectIds.Where(id => states[id] == FabricationReleaseState.Unknown).ToList();
            if (unknown.Count > 0)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedUnknownReleaseState, seriesStart, numbers, unknown);

            var stale = members
                .Where(member => member.Part.IsNumberingUpToDate != true || member.Position.IsOutOfDate)
                .Select(member => member.Part.ObjectId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();
            if (stale.Count > 0)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedNumberingStale, seriesStart, numbers, stale);

            if (!modelNumberingCurrent)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedModelNumberingNotUpToDate, seriesStart, numbers, Array.Empty<int>());

            // Checked last so release and staleness findings still surface
            // for a selection; only the would-be proposal is withheld.
            if (!isWholeModel)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedPartialInput, seriesStart, numbers, Array.Empty<int>());

            if (mixedStartObjectIds.Count > 0)
                return Blocked(positionKind, prefix, GapRepairDecision.BlockedMixedStartNumbers, seriesStart, numbers, mixedStartObjectIds);

            var newNumberByOld = numbers
                .Select((number, index) => new { Old = number, New = seriesStart + index })
                .ToDictionary(pair => pair.Old, pair => pair.New);

            var changes = members
                .Where(member => newNumberByOld[member.Position.Number!.Value] != member.Position.Number!.Value)
                .GroupBy(member => member.Position.Position, StringComparer.Ordinal)
                .Select(group =>
                {
                    var oldNumber = group.First().Position.Number!.Value;
                    var newNumber = newNumberByOld[oldNumber];
                    return new GapRepairChange(
                        group.Key,
                        Renumber(group.Key, newNumber),
                        oldNumber,
                        newNumber,
                        group.Select(member => member.Part.ObjectId).Distinct().OrderBy(id => id).ToList());
                })
                .OrderBy(change => change.OldNumber)
                .ThenBy(change => change.OldPosition, StringComparer.Ordinal)
                .ToList();

            return new GapRepairSeries(
                positionKind, prefix, GapRepairDecision.Proposed, seriesStart, numbers, changes, Array.Empty<int>());
        }

        private static GapRepairSeries Blocked(
            string positionKind,
            string prefix,
            GapRepairDecision decision,
            int seriesStart,
            IReadOnlyList<int> numbers,
            IReadOnlyList<int> blockingObjectIds) =>
            new GapRepairSeries(
                positionKind, prefix, decision, seriesStart, numbers, Array.Empty<GapRepairChange>(), blockingObjectIds);

        /// <summary>
        /// Replaces the trailing number of a parsed position and keeps
        /// everything before it (prefix and separator) exactly. A zero-padded
        /// number keeps its width: PL/007 -> PL/005.
        /// </summary>
        private static string Renumber(string position, int newNumber)
        {
            var digitStart = position.Length;
            while (digitStart > 0 && position[digitStart - 1] >= '0' && position[digitStart - 1] <= '9')
                digitStart--;

            var oldDigits = position.Substring(digitStart);
            var newDigits = newNumber.ToString(CultureInfo.InvariantCulture);
            if (oldDigits.Length > 1 && oldDigits[0] == '0')
                newDigits = newDigits.PadLeft(oldDigits.Length, '0');

            return position.Substring(0, digitStart) + newDigits;
        }
    }
}
