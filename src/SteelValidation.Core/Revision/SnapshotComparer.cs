using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Core.Revision
{
    public enum ObjectChangeKind
    {
        Added = 0,
        Removed = 1,
        Modified = 2
    }

    /// <summary>One field that differs between two states of the same object.</summary>
    public sealed class FieldChange
    {
        public FieldChange(string field, string before, string after)
        {
            Field = field;
            Before = before;
            After = after;
        }

        public string Field { get; }

        /// <summary>Invariant text; empty when the value was not reported.</summary>
        public string Before { get; }

        public string After { get; }
    }

    /// <summary>What happened to one object (by GUID) between two snapshots.</summary>
    public sealed class ObjectChange
    {
        public ObjectChange(
            string guid,
            ObjectChangeKind kind,
            ObjectState? before,
            ObjectState? after,
            IReadOnlyList<FieldChange> fields)
        {
            Guid = guid;
            Kind = kind;
            Before = before;
            After = after;
            Fields = fields;
        }

        public string Guid { get; }

        public ObjectChangeKind Kind { get; }

        public ObjectState? Before { get; }

        public ObjectState? After { get; }

        /// <summary>Changed fields for a modified object, in a fixed order; empty otherwise.</summary>
        public IReadOnlyList<FieldChange> Fields { get; }

        /// <summary>
        /// True when the object was released in either state: the change has
        /// fabrication impact and belongs to change control (NUM-004, REV-001).
        /// </summary>
        public bool AffectsReleasedObject =>
            Before?.ReleaseState == FabricationReleaseState.Released ||
            After?.ReleaseState == FabricationReleaseState.Released;
    }

    public sealed class SnapshotDiff
    {
        public SnapshotDiff(ModelSnapshot before, ModelSnapshot after, IReadOnlyList<ObjectChange> changes)
        {
            Before = before;
            After = after;
            Changes = changes;
        }

        public ModelSnapshot Before { get; }

        public ModelSnapshot After { get; }

        /// <summary>Changed objects only, ordered by GUID (ordinal).</summary>
        public IReadOnlyList<ObjectChange> Changes { get; }

        public bool HasChanges => Changes.Count > 0;
    }

    /// <summary>
    /// Deterministic comparison of two snapshots by GUID: which objects were
    /// added, removed or modified, and which fields changed from what to what.
    /// "Before this, it was X; now it is Y" is the question V4 to V8 ask.
    /// </summary>
    public static class SnapshotComparer
    {
        public const string PartPositionField = "PartPosition";
        public const string AssemblyPositionField = "AssemblyPosition";
        public const string ProfileField = "Profile";
        public const string MaterialField = "Material";
        public const string NameField = "Name";
        public const string ClassField = "Class";
        public const string LengthField = "LengthMm";
        public const string ReleaseStateField = "ReleaseState";

        /// <summary>
        /// Lengths closer than this are equal. It only absorbs floating-point
        /// noise; it is not an engineering tolerance.
        /// </summary>
        public const double DefaultLengthToleranceMm = 0.01;

        public static SnapshotDiff Compare(
            ModelSnapshot before,
            ModelSnapshot after,
            double lengthToleranceMm = DefaultLengthToleranceMm)
        {
            if (before == null)
                throw new ArgumentNullException(nameof(before));
            if (after == null)
                throw new ArgumentNullException(nameof(after));
            if (lengthToleranceMm < 0 || double.IsNaN(lengthToleranceMm))
                throw new ArgumentOutOfRangeException(nameof(lengthToleranceMm));

            var guids = before.Objects.Select(item => item.Guid)
                .Union(after.Objects.Select(item => item.Guid), StringComparer.Ordinal)
                .OrderBy(guid => guid, StringComparer.Ordinal);

            var changes = new List<ObjectChange>();
            foreach (var guid in guids)
            {
                var old = before.Find(guid);
                var current = after.Find(guid);

                if (old == null)
                {
                    changes.Add(new ObjectChange(guid, ObjectChangeKind.Added, null, current, Array.Empty<FieldChange>()));
                    continue;
                }

                if (current == null)
                {
                    changes.Add(new ObjectChange(guid, ObjectChangeKind.Removed, old, null, Array.Empty<FieldChange>()));
                    continue;
                }

                var fields = CompareFields(old, current, lengthToleranceMm);
                if (fields.Count > 0)
                    changes.Add(new ObjectChange(guid, ObjectChangeKind.Modified, old, current, fields));
            }

            return new SnapshotDiff(before, after, changes);
        }

        private static IReadOnlyList<FieldChange> CompareFields(
            ObjectState before,
            ObjectState after,
            double lengthToleranceMm)
        {
            var fields = new List<FieldChange>();
            AddIfDifferent(fields, PartPositionField, before.PartPosition, after.PartPosition);
            AddIfDifferent(fields, AssemblyPositionField, before.AssemblyPosition, after.AssemblyPosition);
            AddIfDifferent(fields, ProfileField, before.Profile, after.Profile);
            AddIfDifferent(fields, MaterialField, before.Material, after.Material);
            AddIfDifferent(fields, NameField, before.Name, after.Name);
            AddIfDifferent(fields, ClassField, before.ObjectClass, after.ObjectClass);

            if (!LengthsEqual(before.LengthMm, after.LengthMm, lengthToleranceMm))
                fields.Add(new FieldChange(LengthField, FormatLength(before.LengthMm), FormatLength(after.LengthMm)));

            if (before.ReleaseState != after.ReleaseState)
            {
                fields.Add(new FieldChange(
                    ReleaseStateField, before.ReleaseState.ToString(), after.ReleaseState.ToString()));
            }

            return fields;
        }

        // Ordinal on purpose: a Cyrillic "К" and a Latin "K" are different
        // values, even though they look the same.
        private static void AddIfDifferent(List<FieldChange> fields, string field, string? before, string? after)
        {
            if (!string.Equals(before ?? string.Empty, after ?? string.Empty, StringComparison.Ordinal))
                fields.Add(new FieldChange(field, before ?? string.Empty, after ?? string.Empty));
        }

        private static bool LengthsEqual(double? before, double? after, double tolerance)
        {
            if (!before.HasValue || !after.HasValue)
                return before.HasValue == after.HasValue;
            return Math.Abs(before.Value - after.Value) <= tolerance;
        }

        private static string FormatLength(double? value) =>
            value.HasValue ? value.Value.ToString("0.###", CultureInfo.InvariantCulture) : string.Empty;
    }
}
