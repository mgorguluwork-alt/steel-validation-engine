using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelValidation.Core.Bolts
{
    public enum BoltDistanceRuleKind { Edge, Pitch }
    public enum BoltDistanceOutcome { Pass, Fail, Unknown, NotApplicable }

    public sealed class BoltCenter
    {
        public BoltCenter(int index, double? xMm, double? yMm, double? zMm)
        { Index = index; XMm = xMm; YMm = yMm; ZMm = zMm; }
        public int Index { get; }
        public double? XMm { get; }
        public double? YMm { get; }
        public double? ZMm { get; }
        public bool IsKnown => XMm.HasValue && YMm.HasValue && ZMm.HasValue &&
            BoltDistanceChecker.IsFinite(XMm.Value) && BoltDistanceChecker.IsFinite(YMm.Value) && BoltDistanceChecker.IsFinite(ZMm.Value);
    }

    /// <summary>Verified closest distance from one bolt center to THIS target plate's actual boundary, not a bounding box.</summary>
    public sealed class BoltEdgeMeasurement
    {
        public BoltEdgeMeasurement(int centerIndex, double? distanceMm, bool isVerified = false, string? unknownReason = null)
        { CenterIndex = centerIndex; DistanceMm = distanceMm; IsVerified = isVerified; UnknownReason = unknownReason ?? string.Empty; }
        public int CenterIndex { get; }
        public double? DistanceMm { get; }
        public bool IsVerified { get; }
        public string UnknownReason { get; }
    }

    public sealed class BoltDistanceGroupInput
    {
        public BoltDistanceGroupInput(int boltGroupId, int targetPartId, double? nominalDiameterMm,
            IEnumerable<BoltCenter>? centers, IEnumerable<BoltEdgeMeasurement>? edges = null,
            bool positionsComplete = false, string? coordinateFrameId = null, bool? hasPhysicalBolts = true)
        {
            BoltGroupId = boltGroupId; TargetPartId = targetPartId; NominalDiameterMm = nominalDiameterMm;
            Centers = Array.AsReadOnly((centers ?? Array.Empty<BoltCenter>()).ToArray());
            Edges = Array.AsReadOnly((edges ?? Array.Empty<BoltEdgeMeasurement>()).ToArray());
            PositionsComplete = positionsComplete; CoordinateFrameId = coordinateFrameId ?? string.Empty; HasPhysicalBolts = hasPhysicalBolts;
        }
        public int BoltGroupId { get; }
        public int TargetPartId { get; }
        /// <summary>Nominal bolt diameter, never hole diameter or hole tolerance.</summary>
        public double? NominalDiameterMm { get; }
        public IReadOnlyList<BoltCenter> Centers { get; }
        public IReadOnlyList<BoltEdgeMeasurement> Edges { get; }
        public bool PositionsComplete { get; }
        public bool? HasPhysicalBolts { get; }
        /// <summary>All group centers must be expressed in this single declared Cartesian frame; no cross-group comparisons.</summary>
        public string CoordinateFrameId { get; }
    }

    public sealed class BoltDistanceFinding
    {
        internal BoltDistanceFinding(BoltDistanceGroupInput group, int index, int? otherIndex, BoltDistanceRuleKind rule,
            double? actual, double? required, BoltDistanceOutcome outcome, string reason, string? detail = null)
        {
            BoltGroupId = group.BoltGroupId; TargetPartId = group.TargetPartId; BoltCenterIndex = index;
            OtherBoltCenterIndex = otherIndex; RuleKind = rule; ActualMm = actual; RequiredMm = required;
            NominalDiameterMm = BoltDistanceChecker.IsPositiveFinite(group.NominalDiameterMm) ? group.NominalDiameterMm : null;
            RequiredDiameterRatio = rule == BoltDistanceRuleKind.Edge ? 2d : 3d;
            if (actual.HasValue && BoltDistanceChecker.IsPositiveFinite(group.NominalDiameterMm))
            { var ratio = actual.Value / group.NominalDiameterMm!.Value; if (BoltDistanceChecker.IsFinite(ratio)) ActualDiameterRatio = ratio; }
            Outcome = outcome; ReasonCode = reason; Detail = detail ?? string.Empty;
        }
        public int BoltGroupId { get; }
        public int TargetPartId { get; }
        /// <summary>Zero-based adapter center index; -1 only for a group with no identifiable center.</summary>
        public int BoltCenterIndex { get; }
        public int? OtherBoltCenterIndex { get; }
        public BoltDistanceRuleKind RuleKind { get; }
        public BoltDistanceOutcome Outcome { get; }
        public double? NominalDiameterMm { get; }
        public double? ActualMm { get; }
        public double? RequiredMm { get; }
        public double? ActualDiameterRatio { get; }
        public double RequiredDiameterRatio { get; }
        public string ReasonCode { get; }
        public string Detail { get; }
    }

    public sealed class BoltDistanceReport
    {
        internal BoltDistanceReport(List<BoltDistanceFinding> items, List<BoltDistanceFinding> closestPairs, int groups)
        { Items = items.AsReadOnly(); ClosestPitchPairs = closestPairs.AsReadOnly(); GroupsScanned = groups; }
        public IReadOnlyList<BoltDistanceFinding> Items { get; }
        public IReadOnlyList<BoltDistanceFinding> ClosestPitchPairs { get; }
        public int GroupsScanned { get; }
        public int CountsPass => Items.Count(x => x.Outcome == BoltDistanceOutcome.Pass);
        public int CountsFail => Items.Count(x => x.Outcome == BoltDistanceOutcome.Fail);
        public int CountsUnknown => Items.Count(x => x.Outcome == BoltDistanceOutcome.Unknown);
        public int CountsNotApplicable => Items.Count(x => x.Outcome == BoltDistanceOutcome.NotApplicable);
        public string CoverageCode => "WithinBoltGroupOnly";
        /// <summary>Only the supplied group/target scopes; never a cross-group or fabrication verdict.</summary>
        public bool AllChecksPassed => Items.Count > 0 && Items.All(x => x.Outcome == BoltDistanceOutcome.Pass);
        public const string PolicyDescription = "Sirket on tarama kurali: kenar >= 2d, ayni grup ici merkez mesafesi >= 3d. d nominal civata capidir. Yonetmelik uygunlugu veya uretim onayi degildir; farkli gruplar arasi mesafe kontrol edilmez. Sayisal yuvarlama payi en fazla 0.0000001 mm olup imalat toleransi degildir.";
    }

    /// <summary>Pure company screening over verified measurements. Equality passes; unknown evidence never passes.</summary>
    public static class BoltDistanceChecker
    {
        public const double NumericEpsilonMm = 0.0000001d;
        public static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool IsPositiveFinite(double? value) => value.HasValue && IsFinite(value.Value) && value.Value > 0;

        public static BoltDistanceReport Check(IEnumerable<BoltDistanceGroupInput> groups)
        {
            if (groups == null) throw new ArgumentNullException(nameof(groups));
            var items = new List<BoltDistanceFinding>();
            var closestPairs = new List<BoltDistanceFinding>();
            var list = groups.ToList();
            if (list.Any(g => g == null)) throw new ArgumentException("Groups cannot contain null entries.", nameof(groups));
            foreach (var group in list.OrderBy(g => g.BoltGroupId).ThenBy(g => g.TargetPartId))
                CheckGroup(group, items, closestPairs);
            return new BoltDistanceReport(items, closestPairs, list.Select(g => g.BoltGroupId).Distinct().Count());
        }

        private static void CheckGroup(BoltDistanceGroupInput g, List<BoltDistanceFinding> items, List<BoltDistanceFinding> closestPairs)
        {
            double? EdgeRequired() => Required(g.NominalDiameterMm, 2);
            double? PitchRequired() => Required(g.NominalDiameterMm, 3);
            if (g.Centers.Any(c => c == null || c.Index < 0) || g.Centers.Select(c => c.Index).Distinct().Count() != g.Centers.Count)
            {
                items.Add(new BoltDistanceFinding(g, -1, null, BoltDistanceRuleKind.Edge, null, EdgeRequired(), BoltDistanceOutcome.Unknown, "InvalidCenterIdentity"));
                items.Add(new BoltDistanceFinding(g, -1, null, BoltDistanceRuleKind.Pitch, null, PitchRequired(), BoltDistanceOutcome.Unknown, "InvalidCenterIdentity"));
                return;
            }
            var centers = g.Centers.OrderBy(c => c.Index).ToList();
            if (g.HasPhysicalBolts != true)
            {
                var outcome = g.HasPhysicalBolts == false ? BoltDistanceOutcome.NotApplicable : BoltDistanceOutcome.Unknown;
                foreach (var index in centers.Count == 0 ? new[] { -1 } : centers.Select(c => c.Index))
                {
                    var reason = g.HasPhysicalBolts == false ? "HoleOnlyGroup" : "BoltPresenceUnknown";
                    items.Add(new BoltDistanceFinding(g, index, null, BoltDistanceRuleKind.Edge, null, EdgeRequired(), outcome, reason));
                    items.Add(new BoltDistanceFinding(g, index, null, BoltDistanceRuleKind.Pitch, null, PitchRequired(), outcome, reason));
                }
                return;
            }
            if (centers.Count == 0)
            {
                var outcome = g.PositionsComplete ? BoltDistanceOutcome.NotApplicable : BoltDistanceOutcome.Unknown;
                var reason = g.PositionsComplete ? "NoBolts" : "PositionsIncomplete";
                items.Add(new BoltDistanceFinding(g, -1, null, BoltDistanceRuleKind.Edge, null, EdgeRequired(), outcome, reason));
                items.Add(new BoltDistanceFinding(g, -1, null, BoltDistanceRuleKind.Pitch, null, PitchRequired(), outcome, reason));
                return;
            }
            var identityKnown = g.BoltGroupId > 0 && g.TargetPartId > 0;
            var positionReason = !g.PositionsComplete ? "PositionsIncomplete" : string.IsNullOrWhiteSpace(g.CoordinateFrameId) ? "CoordinateFrameUnknown" : centers.Any(c => !c.IsKnown) ? "PositionUnknown" : string.Empty;
            var canMeasure = identityKnown && positionReason.Length == 0;
            var nearestIndices = canMeasure ? Enumerable.Repeat(-1, centers.Count).ToArray() : Array.Empty<int>();
            var nearestDistances = canMeasure ? Enumerable.Repeat(double.PositiveInfinity, centers.Count).ToArray() : Array.Empty<double>();
            if (canMeasure)
                for (int a = 0; a < centers.Count; a++)
                for (int b = a + 1; b < centers.Count; b++)
                {
                    var distance = Distance(centers[a], centers[b]);
                    if (!distance.HasValue) positionReason = "DistanceOutOfRange";
                    else
                    {
                        if (distance.Value < nearestDistances[a]) { nearestDistances[a] = distance.Value; nearestIndices[a] = b; }
                        if (distance.Value < nearestDistances[b]) { nearestDistances[b] = distance.Value; nearestIndices[b] = a; }
                    }
                }
            BoltDistanceFinding? closest = null;
            for (int a = 0; a < centers.Count; a++)
            {
                var center = centers[a];
                var edges = g.Edges.Where(e => e != null && e.CenterIndex == center.Index).ToList();
                double? actualEdge = edges.Count == 1 && edges[0].IsVerified && edges[0].DistanceMm.HasValue && IsFinite(edges[0].DistanceMm!.Value) && edges[0].DistanceMm!.Value >= 0 ? edges[0].DistanceMm : null;
                var edgeReason = !identityKnown ? "TargetIdentityUnknown" : edges.Count != 1 ? (edges.Count == 0 ? "EdgeUnknown" : "DuplicateEdgeMeasurement") : !edges[0].IsVerified ? "EdgeUnverified" : !actualEdge.HasValue ? "EdgeUnknown" : !EdgeRequired().HasValue ? "NominalDiameterUnknown" : "Measured";
                var edgeOutcome = edgeReason == "Measured" ? (Passes(actualEdge!.Value, EdgeRequired()!.Value) ? BoltDistanceOutcome.Pass : BoltDistanceOutcome.Fail) : BoltDistanceOutcome.Unknown;
                items.Add(new BoltDistanceFinding(g, center.Index, null, BoltDistanceRuleKind.Edge, actualEdge, EdgeRequired(), edgeOutcome, edgeOutcome == BoltDistanceOutcome.Pass && actualEdge < EdgeRequired() ? "NumericBoundary" : edgeReason, edges.Count == 1 ? edges[0].UnknownReason : null));

                if (!identityKnown || positionReason.Length > 0)
                { items.Add(new BoltDistanceFinding(g, center.Index, null, BoltDistanceRuleKind.Pitch, null, PitchRequired(), BoltDistanceOutcome.Unknown, !identityKnown ? "TargetIdentityUnknown" : positionReason)); continue; }
                if (centers.Count < 2)
                { items.Add(new BoltDistanceFinding(g, center.Index, null, BoltDistanceRuleKind.Pitch, null, PitchRequired(), BoltDistanceOutcome.NotApplicable, "NoPitchPair")); continue; }
                int nearest = nearestIndices[a];
                var actual = nearestDistances[a];
                var required = PitchRequired();
                var finding = new BoltDistanceFinding(g, center.Index, centers[nearest].Index, BoltDistanceRuleKind.Pitch, actual, required,
                    !required.HasValue ? BoltDistanceOutcome.Unknown : Passes(actual, required.Value) ? BoltDistanceOutcome.Pass : BoltDistanceOutcome.Fail,
                    required.HasValue ? (actual < required.Value && Passes(actual, required.Value) ? "NumericBoundary" : "Measured") : "NominalDiameterUnknown");
                items.Add(finding);
                if (closest == null || finding.ActualMm < closest.ActualMm) closest = finding;
            }
            if (closest != null) closestPairs.Add(closest);
        }

        private static bool Passes(double actual, double required) => actual >= required || required - actual <= Math.Min(NumericEpsilonMm, required * 0.000000001d);

        private static double? Required(double? diameter, double factor)
        {
            if (!IsPositiveFinite(diameter)) return null;
            var required = diameter!.Value * factor;
            return IsFinite(required) ? required : (double?)null;
        }

        private static double? Distance(BoltCenter a, BoltCenter b)
        {
            var dx = a.XMm!.Value - b.XMm!.Value; var dy = a.YMm!.Value - b.YMm!.Value; var dz = a.ZMm!.Value - b.ZMm!.Value;
            var max = Math.Max(Math.Abs(dx), Math.Max(Math.Abs(dy), Math.Abs(dz)));
            if (!IsFinite(max)) return null;
            if (max == 0) return 0;
            var result = max * Math.Sqrt((dx / max) * (dx / max) + (dy / max) * (dy / max) + (dz / max) * (dz / max));
            return IsFinite(result) ? result : (double?)null;
        }
    }
}
