using System;

namespace Conoscope.Core
{
    internal enum ConoscopeAngularQuantity { Luminance, LuminousIntensity }
    internal enum ConoscopeEmitterSizeMode { CircularDiameter, Area }
    internal enum ConoscopeMirrorDirection { None, TopToBottom, BottomToTop, LeftToRight, RightToLeft }

    /// <summary>Rectangle in the source polar plane: x points right and y points up, in degrees.</summary>
    internal sealed record ConoscopeAngularRegion(double MinX, double MaxX, double MinY, double MaxY)
    {
        public bool Contains(double x, double y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;
    }

    internal sealed record ConoscopeAngularAnalysisOptions
    {
        public ConoscopeAngularQuantity Quantity { get; init; }
        public ConoscopeEmitterSizeMode SizeMode { get; init; }
        public double DiameterMillimeters { get; init; } = 0.5;
        public double AreaSquareMillimeters { get; init; } = 1;
        public ConoscopeMirrorDirection MirrorDirection { get; init; }
        public ConoscopeAngularRegion? MirrorRegion { get; init; }
        public double AreaSquareMeters => SizeMode == ConoscopeEmitterSizeMode.CircularDiameter
            ? Math.PI * Math.Pow(DiameterMillimeters / 2000, 2) : AreaSquareMillimeters * 1e-6;

        public ConoscopeAngularRegion GetMirrorRegion(double maximum) => MirrorRegion ?? DefaultRegion(MirrorDirection, maximum);

        public static ConoscopeAngularRegion DefaultRegion(ConoscopeMirrorDirection direction, double maximum) => direction switch
        {
            ConoscopeMirrorDirection.TopToBottom => new(-maximum, maximum, -maximum, 0),
            ConoscopeMirrorDirection.BottomToTop => new(-maximum, maximum, 0, maximum),
            ConoscopeMirrorDirection.LeftToRight => new(0, maximum, -maximum, maximum),
            ConoscopeMirrorDirection.RightToLeft => new(-maximum, 0, -maximum, maximum),
            _ => new(-maximum, maximum, -maximum, maximum)
        };

        public void Validate(double maximum)
        {
            if (!Enum.IsDefined(Quantity) || !Enum.IsDefined(SizeMode) || !Enum.IsDefined(MirrorDirection))
                throw new ArgumentException("Unknown angular analysis option.");
            if (!double.IsFinite(maximum) || maximum <= 0 || maximum > 90)
                throw new ArgumentException("Angular analysis requires a half field of view in (0, 90] degrees.");
            if (Quantity == ConoscopeAngularQuantity.LuminousIntensity)
            {
                double size = SizeMode == ConoscopeEmitterSizeMode.CircularDiameter ? DiameterMillimeters : AreaSquareMillimeters;
                if (!double.IsFinite(size) || size <= 0 || !double.IsFinite(AreaSquareMeters) || AreaSquareMeters <= 0)
                    throw new ArgumentException("Emitting diameter or area must be finite and positive.");
            }
            if (MirrorDirection == ConoscopeMirrorDirection.None) return;
            var region = GetMirrorRegion(maximum);
            if (!double.IsFinite(region.MinX) || !double.IsFinite(region.MaxX) || !double.IsFinite(region.MinY) || !double.IsFinite(region.MaxY)
                || region.MinX >= region.MaxX || region.MinY >= region.MaxY
                || region.MinX < -maximum || region.MaxX > maximum || region.MinY < -maximum || region.MaxY > maximum)
                throw new ArgumentException("Mirror rectangle must have positive size and lie inside the angular bounds.");
            bool inTargetHalf = MirrorDirection switch
            {
                ConoscopeMirrorDirection.TopToBottom => region.MaxY <= 0,
                ConoscopeMirrorDirection.BottomToTop => region.MinY >= 0,
                ConoscopeMirrorDirection.LeftToRight => region.MinX >= 0,
                ConoscopeMirrorDirection.RightToLeft => region.MaxX <= 0,
                _ => false
            };
            // A rectangle entirely outside the polar disk cannot select any measured rays.
            double nearestX = Math.Clamp(0, region.MinX, region.MaxX), nearestY = Math.Clamp(0, region.MinY, region.MaxY);
            if (!inTargetHalf || nearestX * nearestX + nearestY * nearestY >= maximum * maximum)
                throw new ArgumentException("Mirror rectangle must intersect the polar disk and stay on the selected target side.");
        }

        public bool TryMirror(double x, double y, double maximum, out double referenceX, out double referenceY)
        {
            referenceX = x;
            referenceY = y;
            if (MirrorDirection == ConoscopeMirrorDirection.None || !GetMirrorRegion(maximum).Contains(x, y)) return false;
            // Do not replace the symmetry axis (including floating-point trig residue).
            const double axisTolerance = 1e-10;
            bool replace = MirrorDirection switch
            {
                ConoscopeMirrorDirection.TopToBottom => y < -axisTolerance,
                ConoscopeMirrorDirection.BottomToTop => y > axisTolerance,
                ConoscopeMirrorDirection.LeftToRight => x > axisTolerance,
                ConoscopeMirrorDirection.RightToLeft => x < -axisTolerance,
                _ => false
            };
            if (!replace) return false;
            if (MirrorDirection is ConoscopeMirrorDirection.TopToBottom or ConoscopeMirrorDirection.BottomToTop) referenceY = -y;
            else referenceX = -x;
            return true;
        }
    }
}
