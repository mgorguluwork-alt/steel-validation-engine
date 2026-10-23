using System;

namespace SteelValidation.Core.Geometry
{
    /// <summary>
    /// A known model-space point expressed in millimetres.
    /// </summary>
    public sealed class GeometryPointMm
    {
        public GeometryPointMm(double x, double y, double z)
        {
            if (!IsFinite(x) || !IsFinite(y) || !IsFinite(z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    "Geometry-point coordinates must be finite.");
            }

            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }

        public double Y { get; }

        public double Z { get; }

        private static bool IsFinite(double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
