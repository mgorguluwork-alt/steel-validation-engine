using System;

namespace SteelValidation.Core.Geometry
{
    /// <summary>
    /// Explicit geometry-rule thresholds, all in millimetres.
    /// </summary>
    public sealed class GeometryCheckOptions
    {
        public GeometryCheckOptions(
            double minimumLengthMm = 10d,
            double duplicateToleranceMm = 1d)
        {
            if (!IsFinite(minimumLengthMm) || minimumLengthMm < 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minimumLengthMm),
                    "The minimum length must be finite and non-negative.");
            }

            if (!IsFinite(duplicateToleranceMm) || duplicateToleranceMm <= 0d)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duplicateToleranceMm),
                    "The duplicate tolerance must be finite and greater than zero.");
            }

            MinimumLengthMm = minimumLengthMm;
            DuplicateToleranceMm = duplicateToleranceMm;
        }

        public double MinimumLengthMm { get; }

        public double DuplicateToleranceMm { get; }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
