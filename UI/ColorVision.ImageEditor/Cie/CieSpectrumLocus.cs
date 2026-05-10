using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Resources;

namespace ColorVision.ImageEditor.Cie
{
    public readonly record struct CieSpectrumPoint(int Wavelength, CieChromaticity Chromaticity);

    public static class CieSpectrumLocus
    {
        private const string ResourceUri = "pack://application:,,,/ColorVision.ImageEditor;component/Assets/Data/CIE_cc_1931_2deg.csv";
        private static IReadOnlyList<CieSpectrumPoint>? _points;

        public static IReadOnlyList<CieSpectrumPoint> Points => _points ??= LoadPoints();

        public static CieSpectrumPoint? FindNearest(int wavelength)
        {
            IReadOnlyList<CieSpectrumPoint> points = Points;
            if (points.Count == 0)
            {
                return null;
            }

            CieSpectrumPoint nearest = points[0];
            int nearestDistance = Math.Abs(nearest.Wavelength - wavelength);
            for (int i = 1; i < points.Count; i++)
            {
                int distance = Math.Abs(points[i].Wavelength - wavelength);
                if (distance < nearestDistance)
                {
                    nearest = points[i];
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static IReadOnlyList<CieSpectrumPoint> LoadPoints()
        {
            StreamResourceInfo? resource = Application.GetResourceStream(new Uri(ResourceUri, UriKind.Absolute));
            if (resource == null)
            {
                return Array.Empty<CieSpectrumPoint>();
            }

            List<CieSpectrumPoint> points = new();
            using Stream stream = resource.Stream;
            using StreamReader reader = new(stream);

            while (reader.ReadLine() is string line)
            {
                string[] columns = line.Split(',');
                if (columns.Length < 3)
                {
                    continue;
                }

                if (!int.TryParse(columns[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int wavelength) ||
                    !double.TryParse(columns[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) ||
                    !double.TryParse(columns[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    continue;
                }

                points.Add(new CieSpectrumPoint(wavelength, new CieChromaticity(x, y)));
            }

            return points;
        }
    }
}
