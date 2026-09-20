using System;
using System.Windows.Media.Media3D;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    /// <summary>Time-based orbit about a stable focus. All dimensions are display-space units.</summary>
    internal sealed class HeightMapOrbitCamera
    {
        private double yaw = -90, pitch = 55, distance = 700;
        private double targetYaw = -90, targetPitch = 55, targetDistance = 700;
        private Point3D focus, targetFocus;
        public const double FieldOfView = 45;
        public Point3D Position { get; private set; }
        public Vector3D LookDirection { get; private set; }
        public Vector3D UpDirection { get; private set; }
        public double Distance => distance;

        public void Fit(Rect3D bounds, double aspect, bool top = false, bool immediate = false)
        {
            if (bounds.IsEmpty) return;
            // Orbit angles accumulate across full turns. Reset to the nearest equivalent
            // heading so Home does not replay those turns during its smooth transition.
            targetYaw = yaw + Math.IEEERemainder(-90 - yaw, 360);
            targetPitch = top ? 89.9 : 55;
            targetFocus = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            targetDistance = FitDistance(bounds, aspect, targetYaw, targetPitch);
            if (immediate) Snap();
        }

        internal static double FitDistance(Rect3D bounds, double aspect, double azimuth, double elevation)
        {
            var (outward, right, up) = Basis(azimuth, elevation);
            var center = new Point3D(bounds.X + bounds.SizeX / 2, bounds.Y + bounds.SizeY / 2, bounds.Z + bounds.SizeZ / 2);
            // Helix SharpDX uses a vertical field of view. Project every bounding corner,
            // including its camera-space depth; a width/height-only fit clips tall peaks.
            double tanY = Math.Tan(FieldOfView * Math.PI / 360);
            double tanX = tanY * (double.IsFinite(aspect) && aspect > 0 ? aspect : 1);
            double result = 1;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Point3D(bounds.X + ((i & 1) == 0 ? 0 : bounds.SizeX),
                    bounds.Y + ((i & 2) == 0 ? 0 : bounds.SizeY), bounds.Z + ((i & 4) == 0 ? 0 : bounds.SizeZ));
                Vector3D delta = corner - center;
                double depth = Vector3D.DotProduct(delta, outward);
                result = Math.Max(result, depth + Math.Max(Math.Abs(Vector3D.DotProduct(delta, right)) / tanX,
                    Math.Abs(Vector3D.DotProduct(delta, up)) / tanY) * 1.15);
            }
            return result;
        }

        public void Orbit(double xDegrees, double yDegrees)
        {
            targetYaw -= xDegrees;
            targetPitch = Math.Clamp(targetPitch + yDegrees, -85, 89.9);
        }

        public void Zoom(double wheelSteps) => targetDistance = Math.Clamp(targetDistance * Math.Exp(-wheelSteps * 0.13), 0.5, 100000);

        public void Pan(double dx, double dy, double viewportHeight)
        {
            var (_, right, up) = Basis(yaw, pitch);
            double unitsPerPixel = 2 * distance * Math.Tan(FieldOfView * Math.PI / 360) / Math.Max(viewportHeight, 1);
            targetFocus += (-right * dx + up * dy) * unitsPerPixel;
        }

        public bool Update(double seconds)
        {
            double weight = 1 - Math.Exp(-Math.Clamp(seconds, 0, 0.1) / 0.055);
            yaw += (targetYaw - yaw) * weight;
            pitch += (targetPitch - pitch) * weight;
            distance += (targetDistance - distance) * weight;
            focus += (targetFocus - focus) * weight;
            bool moving = Math.Abs(targetYaw - yaw) + Math.Abs(targetPitch - pitch) > 0.015
                || Math.Abs(targetDistance - distance) > 0.005 || (targetFocus - focus).Length > 0.005;
            if (!moving) Snap();
            else Apply();
            return moving;
        }

        private void Snap()
        {
            yaw = targetYaw; pitch = targetPitch; distance = targetDistance; focus = targetFocus;
            Apply();
        }

        private void Apply()
        {
            var (outward, _, up) = Basis(yaw, pitch);
            Position = focus + outward * distance;
            LookDirection = focus - Position;
            UpDirection = up;
        }

        private static (Vector3D Outward, Vector3D Right, Vector3D Up) Basis(double azimuth, double elevation)
        {
            double y = azimuth * Math.PI / 180, p = elevation * Math.PI / 180;
            var outward = new Vector3D(Math.Cos(y) * Math.Cos(p), Math.Sin(y) * Math.Cos(p), Math.Sin(p));
            var right = new Vector3D(-Math.Sin(y), Math.Cos(y), 0);
            return (outward, right, Vector3D.CrossProduct(outward, right));
        }
    }
}
