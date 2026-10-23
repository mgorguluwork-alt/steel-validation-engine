using System;
using System.Collections.Generic;
using System.Linq;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Core.Revision
{
    /// <summary>
    /// The traceable old -> new record for one object whose position changes
    /// (RULES NUM-005): GUID, assembly, old and new position, time, who, why
    /// and the release state at that moment.
    /// </summary>
    public sealed class PositionChangeRecord
    {
        public PositionChangeRecord(
            string guid,
            int objectId,
            string positionKind,
            string assemblyPosition,
            string oldPosition,
            string newPosition,
            DateTime timestampUtc,
            string by,
            string reason,
            FabricationReleaseState releaseState)
        {
            Guid = guid;
            ObjectId = objectId;
            PositionKind = positionKind;
            AssemblyPosition = assemblyPosition;
            OldPosition = oldPosition;
            NewPosition = newPosition;
            TimestampUtc = timestampUtc;
            By = by;
            Reason = reason;
            ReleaseState = releaseState;
        }

        public string Guid { get; }

        public int ObjectId { get; }

        public string PositionKind { get; }

        /// <summary>The assembly position the object had when recorded.</summary>
        public string AssemblyPosition { get; }

        public string OldPosition { get; }

        public string NewPosition { get; }

        public DateTime TimestampUtc { get; }

        public string By { get; }

        public string Reason { get; }

        public FabricationReleaseState ReleaseState { get; }
    }

    public static class PositionChangeRecords
    {
        /// <summary>
        /// The records a proposed gap repair would write, one per object and
        /// change, taking GUIDs and assemblies from the snapshot the plan was
        /// made from. Blocked series produce none. If an object in the plan
        /// has no state in the snapshot the change cannot be traced, so this
        /// refuses rather than write a record without a GUID.
        /// </summary>
        public static IReadOnlyList<PositionChangeRecord> FromPlan(
            GapRepairPlan plan,
            ModelSnapshot snapshot,
            DateTime timestampUtc,
            string by)
        {
            if (plan == null)
                throw new ArgumentNullException(nameof(plan));
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (timestampUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("The record time must be UTC.", nameof(timestampUtc));
            if (string.IsNullOrWhiteSpace(by))
                throw new ArgumentException("A record must say who made the change.", nameof(by));

            var byObjectId = new Dictionary<int, ObjectState>();
            foreach (var state in snapshot.Objects)
            {
                if (byObjectId.ContainsKey(state.ObjectId))
                {
                    throw new InvalidOperationException(
                        "Object id " + state.ObjectId + " appears twice in the snapshot; changes cannot be traced to one GUID.");
                }

                byObjectId.Add(state.ObjectId, state);
            }

            var records = new List<PositionChangeRecord>();
            foreach (var series in plan.Series.Where(item => item.Decision == GapRepairDecision.Proposed))
            {
                foreach (var change in series.Changes)
                {
                    foreach (var objectId in change.ObjectIds)
                    {
                        if (!byObjectId.TryGetValue(objectId, out var state))
                        {
                            throw new InvalidOperationException(
                                "Object " + objectId + " is in the plan but not in the snapshot; its GUID is unknown, so the change cannot be traced (NUM-005).");
                        }

                        // Defence in depth: a proposal is only ever made for
                        // unreleased objects, so any other state here means
                        // the plan and the snapshot disagree.
                        if (state.ReleaseState != FabricationReleaseState.NotReleased)
                        {
                            throw new InvalidOperationException(
                                "Object " + objectId + " (" + state.Guid + ") is " + state.ReleaseState +
                                " in the snapshot but its position is in a proposed change; the plan and the snapshot disagree.");
                        }

                        records.Add(new PositionChangeRecord(
                            state.Guid,
                            objectId,
                            series.PositionKind,
                            state.AssemblyPosition ?? string.Empty,
                            change.OldPosition,
                            change.NewPosition,
                            timestampUtc,
                            by.Trim(),
                            change.Reason,
                            state.ReleaseState));
                    }
                }
            }

            return records;
        }
    }
}
