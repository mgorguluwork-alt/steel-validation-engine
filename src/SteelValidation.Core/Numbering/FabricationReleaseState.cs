using System;
using System.Collections.Generic;

namespace SteelValidation.Core.Numbering
{
    /// <summary>
    /// Fabrication release state of one model object, the input to the V4
    /// gap decision (RULES NUM-002). The model has no verified release data
    /// source yet, so <see cref="Unknown"/> is the default and is treated as
    /// unsafe: positions are never shifted for it.
    /// </summary>
    public enum FabricationReleaseState
    {
        Unknown = 0,
        NotReleased = 1,
        Released = 2
    }

    /// <summary>
    /// Supplies the fabrication release state of model objects. The Tekla
    /// adapter will implement it once the owner confirms where the release
    /// state is kept (UDA, phase, status or an external register).
    /// </summary>
    public interface IReleaseStateSource
    {
        FabricationReleaseState GetState(int objectId);
    }

    /// <summary>
    /// The safe default while no release data source is verified: every
    /// object is <see cref="FabricationReleaseState.Unknown"/>, so no gap is
    /// ever proposed for repair.
    /// </summary>
    public sealed class UnknownReleaseStateSource : IReleaseStateSource
    {
        public static readonly UnknownReleaseStateSource Instance = new UnknownReleaseStateSource();

        private UnknownReleaseStateSource()
        {
        }

        public FabricationReleaseState GetState(int objectId) => FabricationReleaseState.Unknown;
    }

    /// <summary>
    /// One release state for every object of one model, declared by the owner
    /// rather than read from the model: for example "this is an R&amp;D test
    /// model, nothing has been released" (Mehmet, 2026-09-29, New model 1).
    /// Who declared it, when, for which model and why are kept, so any plan
    /// built on it can be traced back to the declaration.
    /// </summary>
    public sealed class OwnerDeclaredReleaseStateSource : IReleaseStateSource
    {
        public OwnerDeclaredReleaseStateSource(
            FabricationReleaseState state,
            string modelName,
            string declaredBy,
            DateTime declaredAtUtc,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("A declaration applies to one named model.", nameof(modelName));
            if (string.IsNullOrWhiteSpace(declaredBy))
                throw new ArgumentException("A declaration must say who made it.", nameof(declaredBy));
            if (declaredAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The declaration time must be UTC.", nameof(declaredAtUtc));
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("A declaration must say why.", nameof(reason));

            State = state;
            ModelName = modelName.Trim();
            DeclaredBy = declaredBy.Trim();
            DeclaredAtUtc = declaredAtUtc;
            Reason = reason.Trim();
        }

        public FabricationReleaseState State { get; }

        public string ModelName { get; }

        public string DeclaredBy { get; }

        public DateTime DeclaredAtUtc { get; }

        public string Reason { get; }

        public FabricationReleaseState GetState(int objectId) => State;

        /// <summary>
        /// True only for the model the declaration names (exact, ordinal) and
        /// for its dated Save As copies named "&lt;model&gt;_yyyy-MM-dd", the
        /// owner's rule for the copy made before any numbering run. A
        /// declaration for one model is never reused for another.
        /// </summary>
        public bool AppliesTo(string? modelName)
        {
            if (modelName == null)
                return false;

            var name = modelName.Trim();
            if (string.Equals(name, ModelName, StringComparison.Ordinal))
                return true;

            var datedPrefix = ModelName + "_";
            return name.StartsWith(datedPrefix, StringComparison.Ordinal) &&
                DateTime.TryParseExact(
                    name.Substring(datedPrefix.Length),
                    "yyyy-MM-dd",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out _);
        }
    }

    /// <summary>
    /// Release states from an explicit map, for example read from a release
    /// register. Objects missing from the map are <see cref="FabricationReleaseState.Unknown"/>.
    /// </summary>
    public sealed class MapReleaseStateSource : IReleaseStateSource
    {
        private readonly Dictionary<int, FabricationReleaseState> _states;

        public MapReleaseStateSource(IEnumerable<KeyValuePair<int, FabricationReleaseState>> states)
        {
            if (states == null)
                throw new ArgumentNullException(nameof(states));

            _states = new Dictionary<int, FabricationReleaseState>();
            foreach (var pair in states)
                _states[pair.Key] = pair.Value;
        }

        public FabricationReleaseState GetState(int objectId) =>
            _states.TryGetValue(objectId, out var state) ? state : FabricationReleaseState.Unknown;
    }
}
