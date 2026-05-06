using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieDiagramProfile
    {
        public CieDiagramProfile(CieDiagramKind kind, string name, string backgroundUri, Rect axisBounds, Rect plotAreaPixels)
        {
            Kind = kind;
            Name = name;
            BackgroundUri = backgroundUri;
            AxisBounds = axisBounds;
            PlotAreaPixels = plotAreaPixels;
        }

        public CieDiagramKind Kind { get; }

        public string Name { get; }

        public string BackgroundUri { get; }

        public Rect AxisBounds { get; }

        public Rect PlotAreaPixels { get; }

        public CieChromaticity ToDiagramPoint(CieChromaticity xy)
        {
            return Kind == CieDiagramKind.Cie1976uv
                ? CieColorConverter.XyToCie1976uv(xy)
                : xy;
        }

        public Point ToImagePixel(CieChromaticity xy)
        {
            return DiagramPointToImagePixel(ToDiagramPoint(xy));
        }

        public Point DiagramPointToImagePixel(CieChromaticity diagramPoint)
        {
            if (!diagramPoint.IsFinite || AxisBounds.Width <= 0 || AxisBounds.Height <= 0)
            {
                return new Point(double.NaN, double.NaN);
            }

            double normalizedX = (diagramPoint.X - AxisBounds.X) / AxisBounds.Width;
            double normalizedY = (diagramPoint.Y - AxisBounds.Y) / AxisBounds.Height;

            return new Point(
                PlotAreaPixels.Left + normalizedX * PlotAreaPixels.Width,
                PlotAreaPixels.Bottom - normalizedY * PlotAreaPixels.Height);
        }

        public bool ContainsDiagramPoint(CieChromaticity diagramPoint)
        {
            return diagramPoint.IsFinite &&
                   diagramPoint.X >= AxisBounds.Left &&
                   diagramPoint.X <= AxisBounds.Right &&
                   diagramPoint.Y >= AxisBounds.Top &&
                   diagramPoint.Y <= AxisBounds.Bottom;
        }
    }

    public static class CieDiagramProfiles
    {
        public static readonly CieDiagramProfile Cie1931xy = new(
            CieDiagramKind.Cie1931xy,
            "CIE 1931 xy",
            "pack://application:,,,/ColorVision.ImageEditor;component/Assets/Image/CIE1931xy.png",
            new Rect(0, 0, 0.8, 0.9),
            new Rect(60, 9.5, 604, 679.5));

        public static readonly CieDiagramProfile Cie1976uv = new(
            CieDiagramKind.Cie1976uv,
            "CIE 1976 u'v'",
            "pack://application:,,,/ColorVision.ImageEditor;component/Assets/Image/CIE_1976_UCS.png",
            new Rect(0, 0, 0.6, 0.6),
            new Rect(104, 140, 1840, 1840));

        private static readonly Dictionary<CieDiagramKind, CieDiagramProfile> Profiles =
            new Dictionary<CieDiagramKind, CieDiagramProfile>
            {
                [CieDiagramKind.Cie1931xy] = Cie1931xy,
                [CieDiagramKind.Cie1976uv] = Cie1976uv
            };

        public static CieDiagramProfile Get(CieDiagramKind kind)
        {
            if (Profiles.TryGetValue(kind, out CieDiagramProfile? profile))
            {
                return profile;
            }

            throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
        }
    }
}
