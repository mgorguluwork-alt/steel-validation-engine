using System;
using System.Collections.Generic;
using System.Linq;

namespace SteelValidation.Core.Bolts
{
    /// <summary>
    /// Distance from a bolt axis normal to a verified straight, convex, planar outer plate contour.
    /// The adapter must first exclude chamfers, holes, Boolean cuts and other boundary modifiers.
    /// This helper never substitutes a bounding box. Unsupported or uncertain geometry returns null.
    /// </summary>
    public static class PlanarBoltEdge
    {
        private const double ToleranceMm = 0.000001d;
        public static double? Measure(IReadOnlyList<BoltCenter> contour, BoltCenter bolt, BoltCenter axisDirection)
        {
            if (contour == null || bolt == null || axisDirection == null || !bolt.IsKnown || !axisDirection.IsKnown ||
                contour.Count < 3 || contour.Any(p => p == null || !p.IsKnown)) return null;
            var points = contour.Select(ToVector).ToList();
            if (Norm(Sub(points[0], points[points.Count - 1])) == 0) points.RemoveAt(points.Count - 1);
            if (points.Count < 3 || !Unit(Sub(points[1], points[0]), out var ux)) return null;
            Vec normal = default(Vec); bool foundNormal = false;
            for (int i = 2; i < points.Count && !foundNormal; i++)
                foundNormal = Unit(Cross(ux, Sub(points[i], points[0])), out normal);
            if (!foundNormal || !Unit(Cross(normal, ux), out var uy) || !Unit(ToVector(axisDirection), out var axis)) return null;
            var parallel = Math.Abs(Dot(normal, axis));
            // Only normal bolts are supported: a tilted axis can have a different edge distance on the two physical faces.
            // This angular bound admits numerical roundoff, not intentional skew holes.
            var normalError = Norm(Cross(normal, axis));
            if (!BoltDistanceChecker.IsFinite(parallel) || !BoltDistanceChecker.IsFinite(normalError) || normalError > 0.000000000001d) return null;
            var polygon = new List<P2>();
            foreach (var p in points)
            {
                var relative = Sub(p, points[0]);
                var offPlane = Dot(relative, normal);
                var x = Dot(relative, ux); var y = Dot(relative, uy);
                if (!BoltDistanceChecker.IsFinite(offPlane) || Math.Abs(offPlane) > ToleranceMm ||
                    !BoltDistanceChecker.IsFinite(x) || !BoltDistanceChecker.IsFinite(y)) return null;
                polygon.Add(new P2(x, y));
            }
            double span = polygon.Max(p => Math.Max(Math.Abs(p.X), Math.Abs(p.Y)));
            if (!BoltDistanceChecker.IsFinite(span) || span <= ToleranceMm) return null;
            double areaTolerance = ToleranceMm * span;
            int winding = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count]; var c = polygon[(i + 2) % polygon.Count];
                if (Distance(a, b) <= ToleranceMm) return null;
                double turn = Cross2(a, b, c);
                if (!BoltDistanceChecker.IsFinite(turn)) return null;
                if (Math.Abs(turn) <= areaTolerance) continue;
                int sign = turn > 0 ? 1 : -1;
                if (winding != 0 && winding != sign) return null; // non-convex: unsupported, never guessed
                winding = sign;
            }
            if (winding == 0) return null;
            for (int i = 0; i < polygon.Count; i++)
            for (int j = i + 1; j < polygon.Count; j++)
            {
                if (j == i + 1 || (i == 0 && j == polygon.Count - 1)) continue;
                if (Intersect(polygon[i], polygon[(i + 1) % polygon.Count], polygon[j], polygon[(j + 1) % polygon.Count], areaTolerance)) return null;
            }
            var boltRelative = Sub(ToVector(bolt), points[0]);
            // Even an almost-normal axis can shift the center in-plane when the input point lies far from this plate.
            // Intersect the actual bolt axis with the target plane; orthogonal projection could falsely pass 2d.
            var along = -Dot(boltRelative, normal) / Dot(axis, normal);
            if (!BoltDistanceChecker.IsFinite(along)) return null;
            var intersection = new Vec(boltRelative.X + along * axis.X, boltRelative.Y + along * axis.Y, boltRelative.Z + along * axis.Z);
            var projected = new P2(Dot(intersection, ux), Dot(intersection, uy));
            if (!BoltDistanceChecker.IsFinite(projected.X) || !BoltDistanceChecker.IsFinite(projected.Y)) return null;
            double closest = double.PositiveInfinity;
            for (int i = 0; i < polygon.Count; i++)
            {
                var a = polygon[i]; var b = polygon[(i + 1) % polygon.Count];
                var side = Cross2(a, b, projected);
                if (!BoltDistanceChecker.IsFinite(side) || side * winding < 0) return null;
                var dx = b.X - a.X; var dy = b.Y - a.Y;
                var denominator = dx * dx + dy * dy;
                if (!BoltDistanceChecker.IsFinite(denominator) || denominator <= 0) return null;
                var t = ((projected.X - a.X) * dx + (projected.Y - a.Y) * dy) / denominator;
                if (!BoltDistanceChecker.IsFinite(t)) return null;
                t = Math.Max(0, Math.Min(1, t));
                var distance = Distance(projected, new P2(a.X + t * dx, a.Y + t * dy));
                if (!BoltDistanceChecker.IsFinite(distance)) return null;
                closest = Math.Min(closest, distance);
            }
            return BoltDistanceChecker.IsFinite(closest) ? closest : (double?)null;
        }

        private static bool Intersect(P2 a, P2 b, P2 c, P2 d, double eps)
        {
            var x1 = Cross2(a, b, c); var x2 = Cross2(a, b, d); var x3 = Cross2(c, d, a); var x4 = Cross2(c, d, b);
            if (!BoltDistanceChecker.IsFinite(x1) || !BoltDistanceChecker.IsFinite(x2) || !BoltDistanceChecker.IsFinite(x3) || !BoltDistanceChecker.IsFinite(x4)) return true;
            if (((x1 > eps && x2 < -eps) || (x1 < -eps && x2 > eps)) && ((x3 > eps && x4 < -eps) || (x3 < -eps && x4 > eps))) return true;
            return (Math.Abs(x1) <= eps && On(a, b, c)) || (Math.Abs(x2) <= eps && On(a, b, d)) ||
                (Math.Abs(x3) <= eps && On(c, d, a)) || (Math.Abs(x4) <= eps && On(c, d, b));
        }
        private static bool On(P2 a, P2 b, P2 p) => p.X >= Math.Min(a.X, b.X) - ToleranceMm && p.X <= Math.Max(a.X, b.X) + ToleranceMm &&
            p.Y >= Math.Min(a.Y, b.Y) - ToleranceMm && p.Y <= Math.Max(a.Y, b.Y) + ToleranceMm;
        private static double Cross2(P2 a, P2 b, P2 c) => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);
        private static double Distance(P2 a, P2 b) => Norm(new Vec(a.X - b.X, a.Y - b.Y, 0));
        private static Vec ToVector(BoltCenter p) => new Vec(p.XMm!.Value, p.YMm!.Value, p.ZMm!.Value);
        private static Vec Sub(Vec a, Vec b) => new Vec(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        private static Vec Cross(Vec a, Vec b) => new Vec(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        private static double Dot(Vec a, Vec b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static double Norm(Vec v)
        {
            var max = Math.Max(Math.Abs(v.X), Math.Max(Math.Abs(v.Y), Math.Abs(v.Z)));
            if (max == 0) return 0;
            return max * Math.Sqrt((v.X / max) * (v.X / max) + (v.Y / max) * (v.Y / max) + (v.Z / max) * (v.Z / max));
        }
        private static bool Unit(Vec v, out Vec unit)
        {
            var n = Norm(v); unit = default(Vec);
            if (!BoltDistanceChecker.IsFinite(n) || n <= ToleranceMm) return false;
            unit = new Vec(v.X / n, v.Y / n, v.Z / n); return true;
        }
        private readonly struct Vec
        { public Vec(double x, double y, double z) { X = x; Y = y; Z = z; } public double X { get; } public double Y { get; } public double Z { get; } }
        private readonly struct P2
        { public P2(double x, double y) { X = x; Y = y; } public double X { get; } public double Y { get; } }
    }
}
