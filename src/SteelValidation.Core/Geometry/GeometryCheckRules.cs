using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SteelValidation.Abstractions;

namespace SteelValidation.Core.Geometry
{
    /// <summary>
    /// Deterministic, Tekla-independent rules for the V3 read-only geometry
    /// check. All model data is supplied by the caller; unknown values stay
    /// unknown and are never inferred.
    /// </summary>
    public static class GeometryCheckRules
    {
        public const string UnknownLengthRule = "UNKNOWN_LENGTH";
        public const string ShortOrZeroLengthRule = "SHORT_OR_ZERO_LENGTH";
        public const string EmptyProfileRule = "EMPTY_PROFILE";
        public const string DuplicateBeamRule = "DUPLICATE_BEAM";

        public const string UnknownStatus = "UNKNOWN";
        public const string WarningStatus = "WARNING";

        /// <summary>
        /// Applies all V3 geometry checks. Output order is stable: length
        /// observations by object id, profile observations by object id, then
        /// duplicate groups by their lowest object id.
        /// </summary>
        public static GeometryCheckResult Check(
            IEnumerable<GeometryCheckPart> parts,
            GeometryCheckOptions? options = null)
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

            var effectiveOptions = options ?? new GeometryCheckOptions();
            var orderedParts = inputParts
                .Select((part, index) => new IndexedPart(part, index))
                .OrderBy(item => item.Part.ObjectId)
                .ThenBy(item => item.InputIndex)
                .ToList();

            var result = new GeometryCheckResult();
            var lengthFindings = new List<GeometryCheckFinding>();
            var profileFindings = new List<GeometryCheckFinding>();
            var partsWithEndpoints = new List<IndexedPart>();

            foreach (var item in orderedParts)
            {
                var part = item.Part;

                if (!part.LengthMm.HasValue)
                {
                    result.UnknownLengthCount++;
                    lengthFindings.Add(CreateFinding(
                        UnknownLengthRule,
                        UnknownStatus,
                        part.ObjectId,
                        "Length is unknown and was not assumed to be zero."));
                }
                else if (part.LengthMm.Value < effectiveOptions.MinimumLengthMm)
                {
                    result.ShortOrZeroLengthCount++;
                    lengthFindings.Add(CreateFinding(
                        ShortOrZeroLengthRule,
                        WarningStatus,
                        part.ObjectId,
                        "Length " + FormatMillimetres(part.LengthMm.Value) +
                        " mm is below the minimum " +
                        FormatMillimetres(effectiveOptions.MinimumLengthMm) +
                        " mm."));
                }

                if (string.IsNullOrWhiteSpace(part.Profile))
                {
                    result.EmptyProfileCount++;
                    profileFindings.Add(CreateFinding(
                        EmptyProfileRule,
                        WarningStatus,
                        part.ObjectId,
                        "Profile is empty."));
                }

                if (part.HasKnownEndpoints)
                {
                    result.PartsChecked++;
                    partsWithEndpoints.Add(item);
                }
                else
                {
                    result.PartsNotChecked++;
                }
            }

            result.Findings.AddRange(lengthFindings);
            result.Findings.AddRange(profileFindings);

            foreach (var duplicateGroup in FindDuplicateGroups(
                partsWithEndpoints,
                effectiveOptions.DuplicateToleranceMm))
            {
                result.DuplicateBeamGroupCount++;
                result.Findings.Add(new GeometryCheckFinding
                {
                    Rule = DuplicateBeamRule,
                    Status = WarningStatus,
                    ObjectIds = duplicateGroup,
                    Message = "Beams have the same trimmed profile and coincident endpoints within " +
                        FormatMillimetres(effectiveOptions.DuplicateToleranceMm) +
                        " mm."
                });
            }

            return result;
        }

        private static IEnumerable<List<int>> FindDuplicateGroups(
            List<IndexedPart> parts,
            double toleranceMm)
        {
            var groups = new List<List<int>>();
            if (parts.Count < 2)
                return groups;

            var disjointSet = new DisjointSet(parts.Count);
            var buckets = new Dictionary<BucketKey, List<int>>();

            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var current = parts[partIndex].Part;
                var normalized = NormalizedEndpoints.From(current);
                var center = BucketKey.From(normalized, toleranceMm);

                foreach (var neighbour in center.Neighbours())
                {
                    if (!buckets.TryGetValue(neighbour, out var candidates))
                        continue;

                    foreach (var candidateIndex in candidates)
                    {
                        if (EndpointsCoincide(
                            current,
                            parts[candidateIndex].Part,
                            toleranceMm))
                        {
                            disjointSet.Union(partIndex, candidateIndex);
                        }
                    }
                }

                if (!buckets.TryGetValue(center, out var bucket))
                {
                    bucket = new List<int>();
                    buckets.Add(center, bucket);
                }

                bucket.Add(partIndex);
            }

            var groupsByRoot = new Dictionary<int, List<int>>();
            for (var partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var root = disjointSet.Find(partIndex);
                if (!groupsByRoot.TryGetValue(root, out var group))
                {
                    group = new List<int>();
                    groupsByRoot.Add(root, group);
                }

                group.Add(parts[partIndex].Part.ObjectId);
            }

            foreach (var group in groupsByRoot.Values)
            {
                if (group.Count < 2)
                    continue;

                group.Sort();
                groups.Add(group);
            }

            groups.Sort(CompareGroups);
            return groups;
        }

        private static int CompareGroups(List<int> left, List<int> right)
        {
            var common = Math.Min(left.Count, right.Count);
            for (var index = 0; index < common; index++)
            {
                var comparison = left[index].CompareTo(right[index]);
                if (comparison != 0)
                    return comparison;
            }

            return left.Count.CompareTo(right.Count);
        }

        private static GeometryCheckFinding CreateFinding(
            string rule,
            string status,
            int objectId,
            string message)
        {
            return new GeometryCheckFinding
            {
                Rule = rule,
                Status = status,
                ObjectIds = new List<int> { objectId },
                Message = message
            };
        }

        private static bool EndpointsCoincide(
            GeometryCheckPart first,
            GeometryCheckPart second,
            double toleranceMm)
        {
            return
                (PointsCoincide(
                    first.StartPointMm!, second.StartPointMm!, toleranceMm) &&
                 PointsCoincide(
                    first.EndPointMm!, second.EndPointMm!, toleranceMm)) ||
                (PointsCoincide(
                    first.StartPointMm!, second.EndPointMm!, toleranceMm) &&
                 PointsCoincide(
                    first.EndPointMm!, second.StartPointMm!, toleranceMm));
        }

        private static bool PointsCoincide(
            GeometryPointMm first,
            GeometryPointMm second,
            double toleranceMm)
        {
            var x = first.X - second.X;
            var y = first.Y - second.Y;
            var z = first.Z - second.Z;

            // Check components before normalising so very large finite model
            // coordinates cannot overflow a squared-distance calculation.
            if (Math.Abs(x) > toleranceMm ||
                Math.Abs(y) > toleranceMm ||
                Math.Abs(z) > toleranceMm)
            {
                return false;
            }

            x /= toleranceMm;
            y /= toleranceMm;
            z /= toleranceMm;
            return x * x + y * y + z * z <= 1d;
        }

        private static string NormalizeProfile(string? profile) =>
            profile == null ? string.Empty : profile.Trim();

        private static int ComparePoints(GeometryPointMm left, GeometryPointMm right)
        {
            var x = left.X.CompareTo(right.X);
            if (x != 0)
                return x;

            var y = left.Y.CompareTo(right.Y);
            return y != 0 ? y : left.Z.CompareTo(right.Z);
        }

        private static string FormatMillimetres(double value) =>
            value.ToString("G15", CultureInfo.InvariantCulture);

        private sealed class IndexedPart
        {
            public IndexedPart(GeometryCheckPart part, int inputIndex)
            {
                Part = part;
                InputIndex = inputIndex;
            }

            public GeometryCheckPart Part { get; }

            public int InputIndex { get; }
        }

        /// <summary>
        /// Endpoints are direction-normalised before bucketing so a beam and
        /// the same beam reversed have identical candidate buckets. The bucket
        /// cell has width 2*tolerance; examining its immediate neighbours is
        /// sufficient for any endpoints whose Euclidean distance is at most
        /// tolerance. Exact distance checks remain the final authority.
        /// </summary>
        private sealed class NormalizedEndpoints
        {
            private NormalizedEndpoints(
                string profile,
                GeometryPointMm start,
                GeometryPointMm end)
            {
                Profile = profile;
                Start = start;
                End = end;
            }

            public string Profile { get; }

            public GeometryPointMm Start { get; }

            public GeometryPointMm End { get; }

            public static NormalizedEndpoints From(GeometryCheckPart part)
            {
                var start = part.StartPointMm!;
                var end = part.EndPointMm!;
                return ComparePoints(start, end) <= 0
                    ? new NormalizedEndpoints(NormalizeProfile(part.Profile), start, end)
                    : new NormalizedEndpoints(NormalizeProfile(part.Profile), end, start);
            }
        }

        private sealed class BucketKey : IEquatable<BucketKey>
        {
            private BucketKey(
                string profile,
                long startX,
                long startY,
                long startZ,
                long endX,
                long endY,
                long endZ)
            {
                Profile = profile;
                StartX = startX;
                StartY = startY;
                StartZ = startZ;
                EndX = endX;
                EndY = endY;
                EndZ = endZ;
            }

            public string Profile { get; }
            public long StartX { get; }
            public long StartY { get; }
            public long StartZ { get; }
            public long EndX { get; }
            public long EndY { get; }
            public long EndZ { get; }

            public static BucketKey From(
                NormalizedEndpoints endpoints,
                double toleranceMm)
            {
                return new BucketKey(
                    endpoints.Profile,
                    RoundCoordinate(endpoints.Start.X, toleranceMm),
                    RoundCoordinate(endpoints.Start.Y, toleranceMm),
                    RoundCoordinate(endpoints.Start.Z, toleranceMm),
                    RoundCoordinate(endpoints.End.X, toleranceMm),
                    RoundCoordinate(endpoints.End.Y, toleranceMm),
                    RoundCoordinate(endpoints.End.Z, toleranceMm));
            }

            public IEnumerable<BucketKey> Neighbours()
            {
                for (var startX = -1; startX <= 1; startX++)
                for (var startY = -1; startY <= 1; startY++)
                for (var startZ = -1; startZ <= 1; startZ++)
                for (var endX = -1; endX <= 1; endX++)
                for (var endY = -1; endY <= 1; endY++)
                for (var endZ = -1; endZ <= 1; endZ++)
                {
                    yield return new BucketKey(
                        Profile,
                        StartX + startX,
                        StartY + startY,
                        StartZ + startZ,
                        EndX + endX,
                        EndY + endY,
                        EndZ + endZ);
                }
            }

            public bool Equals(BucketKey? other)
            {
                return other != null &&
                    StringComparer.OrdinalIgnoreCase.Equals(Profile, other.Profile) &&
                    StartX == other.StartX && StartY == other.StartY && StartZ == other.StartZ &&
                    EndX == other.EndX && EndY == other.EndY && EndZ == other.EndZ;
            }

            public override bool Equals(object? obj) => Equals(obj as BucketKey);

            public override int GetHashCode()
            {
                unchecked
                {
                    var hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Profile);
                    hash = hash * 31 + StartX.GetHashCode();
                    hash = hash * 31 + StartY.GetHashCode();
                    hash = hash * 31 + StartZ.GetHashCode();
                    hash = hash * 31 + EndX.GetHashCode();
                    hash = hash * 31 + EndY.GetHashCode();
                    hash = hash * 31 + EndZ.GetHashCode();
                    return hash;
                }
            }

            private static long RoundCoordinate(double value, double toleranceMm)
            {
                var scaled = Math.Round(
                    value / (2d * toleranceMm),
                    MidpointRounding.AwayFromZero);

                if (scaled < long.MinValue + 1d || scaled > long.MaxValue - 1d)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        "Geometry coordinates are outside the supported bucket range.");
                }

                return (long)scaled;
            }
        }

        private sealed class DisjointSet
        {
            private readonly int[] parent;
            private readonly byte[] rank;

            public DisjointSet(int count)
            {
                parent = new int[count];
                rank = new byte[count];
                for (var index = 0; index < count; index++)
                    parent[index] = index;
            }

            public int Find(int value)
            {
                while (parent[value] != value)
                {
                    parent[value] = parent[parent[value]];
                    value = parent[value];
                }

                return value;
            }

            public void Union(int first, int second)
            {
                var firstRoot = Find(first);
                var secondRoot = Find(second);
                if (firstRoot == secondRoot)
                    return;

                if (rank[firstRoot] < rank[secondRoot])
                {
                    parent[firstRoot] = secondRoot;
                }
                else if (rank[firstRoot] > rank[secondRoot])
                {
                    parent[secondRoot] = firstRoot;
                }
                else
                {
                    parent[secondRoot] = firstRoot;
                    rank[firstRoot]++;
                }
            }
        }
    }
}
