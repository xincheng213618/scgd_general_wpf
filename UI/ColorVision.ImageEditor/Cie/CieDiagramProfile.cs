using System;
using System.Collections.Generic;
using System.Windows;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieDiagramProfile
    {
        public CieDiagramProfile(CieDiagramKind kind, string name, string backgroundUri, Rect axisBounds, Rect plotAreaPixels, Size backgroundPixelSize)
        {
            Kind = kind;
            Name = name;
            BackgroundUri = backgroundUri;
            AxisBounds = axisBounds;
            PlotAreaPixels = plotAreaPixels;
            BackgroundPixelSize = backgroundPixelSize;
        }

        public CieDiagramKind Kind { get; }

        public string Name { get; }

        public string BackgroundUri { get; }

        public Rect AxisBounds { get; }

        public Rect PlotAreaPixels { get; }

        public Size BackgroundPixelSize { get; }

        public CieChromaticity ToDiagramPoint(CieChromaticity xy)
        {
            return Kind switch
            {
                CieDiagramKind.Cie1960uv => CieColorConverter.XyToCie1960uv(xy),
                CieDiagramKind.Cie1976uv => CieColorConverter.XyToCie1976uv(xy),
                _ => xy
            };
        }

        public CieChromaticity FromDiagramPoint(CieChromaticity diagramPoint)
        {
            return Kind switch
            {
                CieDiagramKind.Cie1960uv => CieColorConverter.Uv1960ToXy(diagramPoint),
                CieDiagramKind.Cie1976uv => CieColorConverter.Uv1976ToXy(diagramPoint),
                _ => diagramPoint
            };
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

        public CieChromaticity ImagePixelToDiagramPoint(Point imagePixel)
        {
            if (PlotAreaPixels.Width <= 0 || PlotAreaPixels.Height <= 0 || AxisBounds.Width <= 0 || AxisBounds.Height <= 0)
            {
                return CieChromaticity.Empty;
            }

            double normalizedX = (imagePixel.X - PlotAreaPixels.Left) / PlotAreaPixels.Width;
            double normalizedY = (PlotAreaPixels.Bottom - imagePixel.Y) / PlotAreaPixels.Height;

            return new CieChromaticity(
                AxisBounds.X + normalizedX * AxisBounds.Width,
                AxisBounds.Y + normalizedY * AxisBounds.Height);
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
            string.Empty,
            new Rect(0, 0, 0.8, 0.9),
            new Rect(60, 30, 640, 720),
            new Size(760, 800));

        public static readonly CieDiagramProfile Cie1976uv = new(
            CieDiagramKind.Cie1976uv,
            "CIE 1976 u'v'",
            string.Empty,
            new Rect(0, 0, 0.65, 0.6),
            new Rect(70, 40, 715, 660),
            new Size(860, 760));

        public static readonly CieDiagramProfile Cie1960uv = new(
            CieDiagramKind.Cie1960uv,
            "CIE 1960 uv",
            string.Empty,
            new Rect(0, 0, 0.65, 0.4),
            new Rect(70, 40, 715, 440),
            new Size(860, 540));

        private static readonly Dictionary<CieDiagramKind, CieDiagramProfile> Profiles =
            new Dictionary<CieDiagramKind, CieDiagramProfile>
            {
                [CieDiagramKind.Cie1931xy] = Cie1931xy,
                [CieDiagramKind.Cie1960uv] = Cie1960uv,
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
