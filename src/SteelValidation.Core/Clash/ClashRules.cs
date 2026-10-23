using System;

namespace SteelValidation.Core.Clash
{
    /// <summary>
    /// Operator-facing text and severity for a native Tekla clash.
    /// </summary>
    public static class ClashRules
    {
        public const string High = "HIGH";
        public const string Medium = "MEDIUM";
        public const string Low = "LOW";

        public static string GetTypeText(ClashKind kind)
        {
            switch (kind)
            {
                case ClashKind.IsInside: return "INSIDE";
                case ClashKind.ExactMatch: return "EXACT MATCH";
                case ClashKind.Clash: return "CLASH";
                case ClashKind.MinDistance: return "MIN DISTANCE";
                case ClashKind.FailedSolid: return "FAILED SOLID";
                case ClashKind.CutThrough: return "CUT THROUGH";
                case ClashKind.ComplexClash: return "COMPLEX CLASH";
                case ClashKind.FailedTest: return "FAILED TEST";
                default: return "INVALID";
            }
        }

        public static string GetSeverity(ClashKind kind)
        {
            switch (kind)
            {
                case ClashKind.ExactMatch:
                case ClashKind.Clash:
                case ClashKind.CutThrough:
                case ClashKind.ComplexClash:
                case ClashKind.IsInside:
                    return High;

                case ClashKind.MinDistance:
                case ClashKind.FailedSolid:
                case ClashKind.FailedTest:
                    return Medium;

                default:
                    return Low;
            }
        }

        /// <summary>
        /// The overlap is formatted with the current culture, as the adapter
        /// always did (e.g. "12,35 mm overlap" on a Turkish system).
        /// </summary>
        public static string BuildDescription(ClashKind kind, double overlap)
        {
            string overlapText =
                overlap > 0
                    ? Math.Round(overlap, 2) + " mm overlap"
                    : "Geometric conflict";

            switch (kind)
            {
                case ClashKind.IsInside: return "Object is inside another object";
                case ClashKind.ExactMatch: return "Objects occupy the same geometry";
                case ClashKind.Clash: return overlapText;
                case ClashKind.MinDistance: return "Minimum distance violation";
                case ClashKind.FailedSolid: return "Solid geometry could not be checked";
                case ClashKind.CutThrough: return "Object cuts through another object";
                case ClashKind.ComplexClash: return "Complex geometric clash";
                case ClashKind.FailedTest: return "Clash test failed";
                default: return "Unknown clash condition";
            }
        }
    }
}
