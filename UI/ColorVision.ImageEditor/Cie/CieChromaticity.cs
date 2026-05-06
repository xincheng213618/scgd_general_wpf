using System;

namespace ColorVision.ImageEditor.Cie
{
    public readonly record struct CieChromaticity(double X, double Y)
    {
        public bool IsFinite =>
            !double.IsNaN(X) &&
            !double.IsNaN(Y) &&
            !double.IsInfinity(X) &&
            !double.IsInfinity(Y);

        public static CieChromaticity Empty => new(double.NaN, double.NaN);
    }

    public readonly record struct CieXyz(double X, double Y, double Z)
    {
        public bool IsFinite =>
            !double.IsNaN(X) &&
            !double.IsNaN(Y) &&
            !double.IsNaN(Z) &&
            !double.IsInfinity(X) &&
            !double.IsInfinity(Y) &&
            !double.IsInfinity(Z);
    }
}
