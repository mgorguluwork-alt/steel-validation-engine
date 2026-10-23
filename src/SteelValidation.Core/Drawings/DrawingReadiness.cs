using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelValidation.Core.Drawings
{
    /// <summary>How close one drawing is to being issued.</summary>
    public enum DrawingReadinessState
    {
        /// <summary>Up to date and untouched since issue: nothing to do.</summary>
        Ready = 0,

        /// <summary>Out of date for a known reason; updating it in Tekla settles it.</summary>
        NeedsUpdate = 1,

        /// <summary>A person must decide: it cannot be updated, its change reaches fabrication, or its state is unknown.</summary>
        Blocked = 2
    }

    /// <summary>Stable reason codes; the UI maps them to text.</summary>
    public static class DrawingReadinessReason
    {
        public const string NotUpToDate = "NotUpToDate";
        public const string IssuedButModified = "IssuedButModified";
        public const string FrozenAndOutOfDate = "FrozenAndOutOfDate";
        public const string LockedAndOutOfDate = "LockedAndOutOfDate";
        public const string TargetDeleted = "TargetDeleted";

        /// <summary>The status is empty or not one this code knows: no update is prescribed for it.</summary>
        public const string UnknownStatus = "UnknownStatus";
    }

    public sealed class DrawingReadinessItem
    {
        public DrawingReadinessItem(DrawingRecord drawing, DrawingReadinessState state, IReadOnlyList<string> reasons)
        {
            Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
            State = state;
            Reasons = reasons ?? throw new ArgumentNullException(nameof(reasons));
        }

        public DrawingRecord Drawing { get; }

        public DrawingReadinessState State { get; }

        public IReadOnlyList<string> Reasons { get; }
    }

    /// <summary>The drawing-inventory verdict over the whole model. Read-only.</summary>
    public sealed class DrawingReadinessReport
    {
        public IReadOnlyList<DrawingReadinessItem> Items { get; internal set; } = Array.Empty<DrawingReadinessItem>();

        public int ReadyCount { get; internal set; }

        public int NeedsUpdateCount { get; internal set; }

        public int BlockedCount { get; internal set; }

        /// <summary>Part positions that still have no single-part drawing at all.</summary>
        public int MissingPartDrawingCount { get; internal set; }

        /// <summary>Assembly positions that still have no assembly drawing at all.</summary>
        public int MissingAssemblyDrawingCount { get; internal set; }

        /// <summary>Numbering is missing or out of date: marks on drawings would change.</summary>
        public bool NumberingBlocksRelease { get; internal set; }

        /// <summary>
        /// The inventory carries no part positions, so there is nothing to
        /// compare the drawings with; no positive verdict is possible.
        /// </summary>
        public bool ScopeMissing { get; internal set; }

        /// <summary>Inventory and evaluated drawing counts differ; a positive verdict is unsafe.</summary>
        public bool ScopeMismatch { get; internal set; }

        /// <summary>
        /// True only when drawings exist, every one is Ready, the model scope is
        /// known, no position lacks its drawing and numbering is settled. It says
        /// the drawings are current; it does NOT say they are verified or released.
        /// </summary>
        public bool IsInventoryCurrent { get; internal set; }

        /// <summary>
        /// True only when the inventory is current AND the caller supplied explicit
        /// verification/release evidence. Being issued or ready-for-issue in
        /// Tekla is not that evidence.
        /// </summary>
        public bool IsReleaseReady { get; internal set; }
    }

    /// <summary>
    /// V7/V8 starting point: turns the read-only drawing inventory into a
    /// currency verdict. Pure logic over what the model already said; it
    /// changes nothing in Tekla and guesses nothing.
    /// </summary>
    public static class DrawingReadinessRules
    {
        private static readonly string[] DeletedTargetStatuses = { "OriginalPartDeleted", "AllPartsDeleted" };

        // The statuses DrawingInventoryRules.DescribeStatusTurkish knows. Only these
        // may be answered with "update the drawing"; anything else blocks.
        private static readonly string[] KnownOutOfDateStatuses =
        {
            "PartsWereModified",
            "DrawingIsUpToDateButMayNeedChecking",
            "IncludedDrawingModified",
            "OriginalPartDeleted",
            "AllPartsDeleted",
            "NumberOfPartsInNumberingSeriesIncreased",
            "NumberOfPartsInNumberingSeriesDecreased",
            "DrawingWasCloned",
            "DrawingWasUpdated",
            "CopiedViewChanged",
            "DrawingWasSplitted",
            "MovedViewDeleted",
            "MovedViewLabelChanged"
        };

        /// <param name="releaseVerified">
        /// Explicit evidence, from outside the drawing inventory, that the
        /// drawings were verified and released. Defaults to false.
        /// </param>
        public static DrawingReadinessReport Build(
            DrawingInventoryReport inventory,
            IEnumerable<DrawingRecord> drawings,
            bool releaseVerified = false)
        {
            if (inventory == null) throw new ArgumentNullException(nameof(inventory));
            if (drawings == null) throw new ArgumentNullException(nameof(drawings));

            var list = drawings.ToList();
            if (list.Any(d => d == null))
                throw new ArgumentException("The collection cannot contain null entries.", nameof(drawings));

            var items = list
                .Select(Classify)
                .OrderByDescending(item => item.State)
                .ThenBy(item => item.Drawing.Kind)
                .ThenBy(item => item.Drawing.Mark, StringComparer.Ordinal)
                .ThenBy(item => item.Drawing.Name, StringComparer.Ordinal)
                .ToList();

            var report = new DrawingReadinessReport
            {
                Items = items,
                ReadyCount = items.Count(i => i.State == DrawingReadinessState.Ready),
                NeedsUpdateCount = items.Count(i => i.State == DrawingReadinessState.NeedsUpdate),
                BlockedCount = items.Count(i => i.State == DrawingReadinessState.Blocked),
                MissingPartDrawingCount = inventory.PartPositionsWithoutSinglePartDrawing.Count,
                MissingAssemblyDrawingCount = inventory.AssemblyPositionsWithoutAssemblyDrawing.Count,
                NumberingBlocksRelease = inventory.NumberingBlocksDrawingCreation,
                ScopeMissing = inventory.PartPositionCount == 0,
                ScopeMismatch = inventory.TotalDrawings != list.Count
            };

            report.IsInventoryCurrent =
                items.Count > 0 &&
                !report.ScopeMissing &&
                !report.ScopeMismatch &&
                report.ReadyCount == items.Count &&
                report.MissingPartDrawingCount == 0 &&
                report.MissingAssemblyDrawingCount == 0 &&
                !report.NumberingBlocksRelease;

            report.IsReleaseReady = report.IsInventoryCurrent && releaseVerified;

            return report;
        }

        public static DrawingReadinessItem Classify(DrawingRecord drawing)
        {
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));

            var reasons = new List<string>();
            var state = DrawingReadinessState.Ready;

            if (!drawing.IsUpToDate)
            {
                if (!KnownOutOfDateStatuses.Contains(drawing.UpToDateStatus, StringComparer.Ordinal))
                {
                    reasons.Add(DrawingReadinessReason.UnknownStatus);
                    state = DrawingReadinessState.Blocked;
                }
                else
                {
                    reasons.Add(DrawingReadinessReason.NotUpToDate);
                    state = DrawingReadinessState.NeedsUpdate;
                }

                if (drawing.IsFrozen)
                {
                    reasons.Add(DrawingReadinessReason.FrozenAndOutOfDate);
                    state = DrawingReadinessState.Blocked;
                }

                if (drawing.IsLocked)
                {
                    reasons.Add(DrawingReadinessReason.LockedAndOutOfDate);
                    state = DrawingReadinessState.Blocked;
                }

                if (DeletedTargetStatuses.Contains(drawing.UpToDateStatus, StringComparer.Ordinal))
                {
                    reasons.Add(DrawingReadinessReason.TargetDeleted);
                    state = DrawingReadinessState.Blocked;
                }
            }

            if (drawing.IsIssuedButModified)
            {
                reasons.Add(DrawingReadinessReason.IssuedButModified);
                state = DrawingReadinessState.Blocked;
            }

            return new DrawingReadinessItem(drawing, state, reasons);
        }
    }
}
