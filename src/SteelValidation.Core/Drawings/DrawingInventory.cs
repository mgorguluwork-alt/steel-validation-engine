using System;
using System.Collections.Generic;
using System.Linq;
using SteelValidation.Core.Numbering;

namespace SteelValidation.Core.Drawings
{
    /// <summary>The Tekla drawing types (Tekla.Structures.Drawing, 2020).</summary>
    public enum DrawingKind
    {
        SinglePart = 0,
        Assembly = 1,
        GeneralArrangement = 2,
        CastUnit = 3,
        Multi = 4
    }

    /// <summary>
    /// One drawing as read from the model. Tekla-independent; the adapter fills
    /// it from Tekla.Structures.Drawing.Drawing (field names follow the 2020
    /// API). Nothing here is guessed: a value the model did not give stays empty.
    /// </summary>
    public sealed class DrawingRecord
    {
        public DrawingRecord(
            DrawingKind kind,
            string? mark,
            string? name,
            string? upToDateStatus,
            bool isIssued,
            bool isIssuedButModified,
            bool isFrozen,
            bool isLocked,
            bool isReadyForIssue,
            int? targetObjectId)
        {
            Kind = kind;
            Mark = mark ?? string.Empty;
            Name = name ?? string.Empty;
            UpToDateStatus = upToDateStatus ?? string.Empty;
            IsIssued = isIssued;
            IsIssuedButModified = isIssuedButModified;
            IsFrozen = isFrozen;
            IsLocked = isLocked;
            IsReadyForIssue = isReadyForIssue;
            TargetObjectId = targetObjectId;
        }

        public DrawingKind Kind { get; }

        public string Mark { get; }

        public string Name { get; }

        /// <summary>Tekla's DrawingUpToDateStatus name, e.g. "DrawingIsUpToDate".</summary>
        public string UpToDateStatus { get; }

        public bool IsUpToDate => UpToDateStatus == DrawingInventoryRules.UpToDateStatusName;

        /// <summary>Issued for fabrication in Tekla.</summary>
        public bool IsIssued { get; }

        /// <summary>Issued, then changed: the change reaches fabrication.</summary>
        public bool IsIssuedButModified { get; }

        public bool IsFrozen { get; }

        public bool IsLocked { get; }

        public bool IsReadyForIssue { get; }

        /// <summary>
        /// The part id of a single-part drawing, the assembly id of an assembly
        /// drawing, the cast unit id of a cast unit drawing; null otherwise.
        /// </summary>
        public int? TargetObjectId { get; }
    }

    /// <summary>The read-only drawing inventory: what exists and how ready it is.</summary>
    public sealed class DrawingInventoryReport
    {
        public int TotalDrawings { get; internal set; }

        public IReadOnlyDictionary<DrawingKind, int> CountByKind { get; internal set; } =
            new Dictionary<DrawingKind, int>();

        public int NotUpToDateCount { get; internal set; }

        public int IssuedCount { get; internal set; }

        public int IssuedButModifiedCount { get; internal set; }

        public int FrozenCount { get; internal set; }

        public int LockedCount { get; internal set; }

        /// <summary>Distinct current (not "(?)") part positions of the parts read.</summary>
        public int PartPositionCount { get; internal set; }

        /// <summary>Part positions that have a single-part drawing, by the drawing's part id.</summary>
        public int PartPositionsWithSinglePartDrawing { get; internal set; }

        /// <summary>
        /// Part positions without any single-part drawing, ordinal order. A
        /// position listed in PartPositionsWithChangedSinglePartDrawing is not here.
        /// </summary>
        public IReadOnlyList<string> PartPositionsWithoutSinglePartDrawing { get; internal set; } =
            Array.Empty<string>();

        /// <summary>
        /// Part positions whose single-part drawing exists but no longer matches
        /// the part by current position: the drawing's part was changed (its
        /// position is "(?)"), or only the drawing mark carries the position
        /// (Tekla shows such a mark in brackets, e.g. "[PL.26]"). Owner live test
        /// 2026-10-05: these were reported as "no drawing" although Tekla's
        /// drawing list had them. They are settled by numbering, not by a new drawing.
        /// </summary>
        public IReadOnlyList<string> PartPositionsWithChangedSinglePartDrawing { get; internal set; } =
            Array.Empty<string>();

        /// <summary>Distinct current (not "(?)") assembly positions of the parts read.</summary>
        public int AssemblyPositionCount { get; internal set; }

        /// <summary>
        /// Assembly positions none of whose assemblies has an assembly drawing,
        /// ordinal order. Only parts whose assembly id was read take part.
        /// </summary>
        public IReadOnlyList<string> AssemblyPositionsWithoutAssemblyDrawing { get; internal set; } =
            Array.Empty<string>();

        /// <summary>
        /// True when any part or assembly position is missing or out of date:
        /// drawings made now would carry marks that the next numbering run changes.
        /// </summary>
        public bool NumberingBlocksDrawingCreation { get; internal set; }

        /// <summary>
        /// The model part ids of each listed part position, so the operator can
        /// select and zoom to them from the list (owner live test 2026-10-05).
        /// </summary>
        public IReadOnlyDictionary<string, IReadOnlyList<int>> PartIdsByPartPosition { get; internal set; } =
            new Dictionary<string, IReadOnlyList<int>>();

        /// <summary>The model part ids of each listed assembly position.</summary>
        public IReadOnlyDictionary<string, IReadOnlyList<int>> PartIdsByAssemblyPosition { get; internal set; } =
            new Dictionary<string, IReadOnlyList<int>>();

        /// <summary>The model part ids of each assembly id read.</summary>
        public IReadOnlyDictionary<int, IReadOnlyList<int>> PartIdsByAssemblyId { get; internal set; } =
            new Dictionary<int, IReadOnlyList<int>>();

        /// <summary>Drawings that are not up to date or were changed after issue, for the grid.</summary>
        public IReadOnlyList<DrawingRecord> Attention { get; internal set; } = Array.Empty<DrawingRecord>();
    }

    /// <summary>
    /// V5..V8 starting point, read-only: which drawings exist, which are out of
    /// date or were changed after issue, which part positions still have no
    /// single-part drawing, and whether numbering is ready for drawings at all.
    /// </summary>
    public static class DrawingInventoryRules
    {
        public const string UpToDateStatusName = "DrawingIsUpToDate";

        public static DrawingInventoryReport Build(
            IEnumerable<DrawingRecord> drawings,
            IEnumerable<NumberingReportPart> parts)
        {
            if (drawings == null)
                throw new ArgumentNullException(nameof(drawings));
            if (parts == null)
                throw new ArgumentNullException(nameof(parts));

            var drawingList = drawings.ToList();
            var partList = parts.ToList();
            if (drawingList.Any(d => d == null) || partList.Any(p => p == null))
                throw new ArgumentException("The collections cannot contain null entries.");

            var report = new DrawingInventoryReport
            {
                TotalDrawings = drawingList.Count,
                CountByKind = Enum.GetValues(typeof(DrawingKind)).Cast<DrawingKind>()
                    .ToDictionary(kind => kind, kind => drawingList.Count(d => d.Kind == kind)),
                NotUpToDateCount = drawingList.Count(d => !d.IsUpToDate),
                IssuedCount = drawingList.Count(d => d.IsIssued),
                IssuedButModifiedCount = drawingList.Count(d => d.IsIssuedButModified),
                FrozenCount = drawingList.Count(d => d.IsFrozen),
                LockedCount = drawingList.Count(d => d.IsLocked),
                Attention = drawingList
                    .Where(d => !d.IsUpToDate || d.IsIssuedButModified)
                    .OrderBy(d => d.Kind)
                    .ThenBy(d => d.Mark, StringComparer.Ordinal)
                    .ThenBy(d => d.Name, StringComparer.Ordinal)
                    .ToList()
            };

            var positionById = new Dictionary<int, NumberingPosition>();
            foreach (var part in partList)
                positionById[part.ObjectId] = NumberingPosition.Parse(part.PartPosition);

            report.NumberingBlocksDrawingCreation = partList.Any(part =>
            {
                var partPosition = NumberingPosition.Parse(part.PartPosition);
                var assemblyPosition = NumberingPosition.Parse(part.AssemblyPosition);
                return partPosition.IsMissing || partPosition.IsOutOfDate ||
                       assemblyPosition.IsMissing || assemblyPosition.IsOutOfDate ||
                       part.IsNumberingUpToDate != true;
            });

            var currentPositions = positionById.Values
                .Where(position => !position.IsMissing && !position.IsOutOfDate)
                .Select(position => position.Position)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var withDrawing = new HashSet<string>(
                drawingList
                    .Where(d => d.Kind == DrawingKind.SinglePart && d.TargetObjectId.HasValue)
                    .Select(d => positionById.TryGetValue(d.TargetObjectId!.Value, out var position) ? position : null)
                    .Where(position => position != null && !position.IsMissing && !position.IsOutOfDate)
                    .Select(position => position!.Position),
                StringComparer.Ordinal);

            // A drawing whose part changed after numbering ("(?)") or whose mark
            // alone names the position still exists; it is matched by prefix and
            // number, whatever the separator ("PL.26", "[PL.26]", "PL-26").
            var changedDrawingKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var drawing in drawingList.Where(d => d.Kind == DrawingKind.SinglePart))
            {
                if (drawing.TargetObjectId.HasValue &&
                    positionById.TryGetValue(drawing.TargetObjectId.Value, out var target) &&
                    PositionKey(target) is string targetKey)
                    changedDrawingKeys.Add(targetKey);
                if (PositionKey(ParseDrawingMark(drawing.Mark)) is string markKey)
                    changedDrawingKeys.Add(markKey);
            }

            report.PartPositionCount = currentPositions.Count;
            report.PartPositionsWithSinglePartDrawing = currentPositions.Count(withDrawing.Contains);
            var withoutCurrent = currentPositions
                .Where(position => !withDrawing.Contains(position))
                .OrderBy(position => position, StringComparer.Ordinal)
                .ToList();
            report.PartPositionsWithChangedSinglePartDrawing = withoutCurrent
                .Where(position => PositionKey(NumberingPosition.Parse(position)) is string key &&
                                   changedDrawingKeys.Contains(key))
                .ToList();
            report.PartPositionsWithoutSinglePartDrawing = withoutCurrent
                .Except(report.PartPositionsWithChangedSinglePartDrawing, StringComparer.Ordinal)
                .ToList();

            // Assembly side (owner request 2026-10-04: the drawing check must say
            // itself which positions have no drawing): an assembly position has a
            // drawing when any of its assemblies is the target of an assembly drawing.
            var assemblyIdsWithDrawing = new HashSet<int>(
                drawingList
                    .Where(d => d.Kind == DrawingKind.Assembly && d.TargetObjectId.HasValue)
                    .Select(d => d.TargetObjectId!.Value));
            var assemblyPositions = partList
                .Where(part => part.AssemblyObjectId.HasValue)
                .Select(part => (Id: part.AssemblyObjectId!.Value, Position: NumberingPosition.Parse(part.AssemblyPosition)))
                .Where(item => !item.Position.IsMissing && !item.Position.IsOutOfDate)
                .GroupBy(item => item.Position.Position, StringComparer.Ordinal)
                .ToList();
            report.AssemblyPositionCount = assemblyPositions.Count;
            report.AssemblyPositionsWithoutAssemblyDrawing = assemblyPositions
                .Where(group => !group.Any(item => assemblyIdsWithDrawing.Contains(item.Id)))
                .Select(group => group.Key)
                .OrderBy(position => position, StringComparer.Ordinal)
                .ToList();

            report.PartIdsByPartPosition = partList
                .Select(part => (part.ObjectId, Position: NumberingPosition.Parse(part.PartPosition)))
                .Where(item => !item.Position.IsMissing && !item.Position.IsOutOfDate)
                .GroupBy(item => item.Position.Position, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(item => item.ObjectId).ToList(), StringComparer.Ordinal);
            report.PartIdsByAssemblyPosition = partList
                .Select(part => (part.ObjectId, Position: NumberingPosition.Parse(part.AssemblyPosition)))
                .Where(item => !item.Position.IsMissing && !item.Position.IsOutOfDate)
                .GroupBy(item => item.Position.Position, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(item => item.ObjectId).ToList(), StringComparer.Ordinal);
            report.PartIdsByAssemblyId = partList
                .Where(part => part.AssemblyObjectId.HasValue)
                .GroupBy(part => part.AssemblyObjectId!.Value)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<int>)g.Select(part => part.ObjectId).ToList());

            return report;
        }

        /// <summary>
        /// The model part ids a drawing row stands for: the part of a single-part
        /// drawing, the parts of an assembly drawing's assembly; none otherwise.
        /// </summary>
        public static IReadOnlyList<int> PartIdsOf(DrawingInventoryReport report, DrawingRecord drawing)
        {
            if (report == null) throw new ArgumentNullException(nameof(report));
            if (drawing == null) throw new ArgumentNullException(nameof(drawing));
            if (!drawing.TargetObjectId.HasValue) return Array.Empty<int>();
            if (drawing.Kind == DrawingKind.SinglePart) return new[] { drawing.TargetObjectId.Value };
            if (drawing.Kind == DrawingKind.Assembly &&
                report.PartIdsByAssemblyId.TryGetValue(drawing.TargetObjectId.Value, out var ids))
                return ids;
            return Array.Empty<int>();
        }

        // Prefix and number of a position, without separator or "(?)"; null when
        // the position has no number.
        private static string? PositionKey(NumberingPosition position) =>
            position.IsParsed ? position.Prefix + "#" + position.Number : null;

        // A drawing mark such as "[PL.26]" or "PL.26": brackets dropped and the
        // '.' separator read like '-'.
        private static NumberingPosition ParseDrawingMark(string mark)
        {
            var text = (mark ?? string.Empty).Trim().TrimStart('[').TrimEnd(']').Trim();
            var dot = text.LastIndexOf('.');
            if (dot > 0) text = text.Substring(0, dot) + "-" + text.Substring(dot + 1);
            return NumberingPosition.Parse(text);
        }

        /// <summary>
        /// Turkish text for Tekla's DrawingUpToDateStatus name, matching what the
        /// owner sees in Tekla's drawing list "Changes" column. An unknown name is
        /// shown as it is rather than guessed.
        /// </summary>
        public static string DescribeStatusTurkish(string? status)
        {
            switch (status ?? string.Empty)
            {
                case UpToDateStatusName: return "Güncel";
                case "PartsWereModified": return "Parçalar değişti";
                case "DrawingIsUpToDateButMayNeedChecking": return "Güncel, ama kontrol gerekebilir";
                case "IncludedDrawingModified": return "İçindeki çizim değişti";
                case "OriginalPartDeleted": return "Asıl parça silindi";
                case "AllPartsDeleted": return "Tüm parçalar silindi";
                case "NumberOfPartsInNumberingSeriesIncreased": return "Adet arttı";
                case "NumberOfPartsInNumberingSeriesDecreased": return "Adet azaldı";
                case "DrawingWasCloned": return "Klonlandı";
                case "DrawingWasUpdated": return "Güncellendi";
                case "CopiedViewChanged": return "Kopyalanan görünüş değişti";
                case "DrawingWasSplitted": return "Çizim bölündü";
                case "MovedViewDeleted": return "Taşınan görünüş silindi";
                case "MovedViewLabelChanged": return "Taşınan görünüş etiketi değişti";
                default: return string.IsNullOrEmpty(status) ? "Bilinmiyor" : status!;
            }
        }
    }
}
