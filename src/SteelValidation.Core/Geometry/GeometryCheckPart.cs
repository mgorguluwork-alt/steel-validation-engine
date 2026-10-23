using System;

namespace SteelValidation.Core.Geometry
{
    /// <summary>
    /// Tekla-independent input to the geometry rules. Null values are kept as
    /// unknown; the rules never derive substitute geometry or lengths.
    /// </summary>
    public sealed class GeometryCheckPart
    {
        public GeometryCheckPart(
            int objectId,
            string? profile,
            double? lengthMm,
            GeometryPointMm? startPointMm,
            GeometryPointMm? endPointMm)
        {
            if (lengthMm.HasValue &&
                (double.IsNaN(lengthMm.Value) ||
                 double.IsInfinity(lengthMm.Value)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(lengthMm),
                    "A known length must be finite.");
            }

            ObjectId = objectId;
            Profile = profile;
            LengthMm = lengthMm;
            StartPointMm = startPointMm;
            EndPointMm = endPointMm;
        }

        public int ObjectId { get; }

        public string? Profile { get; }

        public double? LengthMm { get; }

        public GeometryPointMm? StartPointMm { get; }

        public GeometryPointMm? EndPointMm { get; }

        public bool HasKnownEndpoints =>
            StartPointMm != null && EndPointMm != null;
    }
}
