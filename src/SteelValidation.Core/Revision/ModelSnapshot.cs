using System;
using System.Collections.Generic;
using System.Linq;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Core.Revision
{
    /// <summary>
    /// The recorded state of one model object. The Tekla GUID is its identity
    /// (RULES: a position number alone is never object identity); the object
    /// id is only valid inside one model session. Values the model did not
    /// report stay null and are never guessed.
    /// </summary>
    public sealed class ObjectState
    {
        public ObjectState(
            string guid,
            int objectId,
            string? partPosition,
            string? assemblyPosition,
            string? profile,
            string? material,
            string? name,
            string? objectClass,
            double? lengthMm,
            FabricationReleaseState releaseState)
        {
            if (string.IsNullOrWhiteSpace(guid))
                throw new ArgumentException("An object state needs a GUID.", nameof(guid));

            Guid = guid.Trim();
            ObjectId = objectId;
            PartPosition = partPosition;
            AssemblyPosition = assemblyPosition;
            Profile = profile;
            Material = material;
            Name = name;
            ObjectClass = objectClass;
            LengthMm = lengthMm;
            ReleaseState = releaseState;
        }

        public string Guid { get; }

        public int ObjectId { get; }

        public string? PartPosition { get; }

        public string? AssemblyPosition { get; }

        public string? Profile { get; }

        public string? Material { get; }

        public string? Name { get; }

        public string? ObjectClass { get; }

        public double? LengthMm { get; }

        public FabricationReleaseState ReleaseState { get; }
    }

    /// <summary>
    /// A point-in-time record of model objects, for revision comparison and
    /// release baselines (RULES REV-001). The caller supplies the time and
    /// the user, so a snapshot never depends on the clock or environment.
    /// </summary>
    public sealed class ModelSnapshot
    {
        private readonly Dictionary<string, ObjectState> _byGuid;

        public ModelSnapshot(
            string modelName,
            DateTime takenAtUtc,
            string takenBy,
            string reason,
            bool isReleaseBaseline,
            IEnumerable<ObjectState> objects)
        {
            if (takenAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The snapshot time must be UTC.", nameof(takenAtUtc));
            if (string.IsNullOrWhiteSpace(takenBy))
                throw new ArgumentException("A snapshot must record who took it.", nameof(takenBy));
            if (objects == null)
                throw new ArgumentNullException(nameof(objects));

            var list = objects.ToList();
            if (list.Any(item => item == null))
                throw new ArgumentException("The object collection cannot contain null entries.", nameof(objects));

            _byGuid = new Dictionary<string, ObjectState>(StringComparer.Ordinal);
            foreach (var state in list)
            {
                if (_byGuid.ContainsKey(state.Guid))
                {
                    throw new ArgumentException(
                        "Duplicate GUID in snapshot: " + state.Guid + ". A GUID identifies exactly one object.",
                        nameof(objects));
                }

                _byGuid.Add(state.Guid, state);
            }

            ModelName = modelName ?? string.Empty;
            TakenAtUtc = takenAtUtc;
            TakenBy = takenBy.Trim();
            Reason = reason ?? string.Empty;
            IsReleaseBaseline = isReleaseBaseline;
            Objects = list.OrderBy(item => item.Guid, StringComparer.Ordinal).ToList();
        }

        public string ModelName { get; }

        public DateTime TakenAtUtc { get; }

        public string TakenBy { get; }

        public string Reason { get; }

        /// <summary>
        /// True for the snapshot taken when objects were released for
        /// fabrication. A later state must never overwrite it (REV-001).
        /// </summary>
        public bool IsReleaseBaseline { get; }

        /// <summary>Objects ordered by GUID (ordinal).</summary>
        public IReadOnlyList<ObjectState> Objects { get; }

        public ObjectState? Find(string guid) =>
            guid != null && _byGuid.TryGetValue(guid.Trim(), out var state) ? state : null;
    }
}
