using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    /// <summary>Appends measured grid lines and corner-to-corner reference chords to existing point markers.</summary>
    public static class GridDistortionOverlayRenderer
    {
        public static void AppendLines(DrawEditorContext drawContext, IReadOnlyList<Point> points, int rows, int columns, string tag)
        {
            ArgumentNullException.ThrowIfNull(drawContext);
            ArgumentNullException.ThrowIfNull(points);
            if (rows < 2 || columns < 2 || (long)rows * columns != points.Count
                || points.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y))
                || points.Distinct().Count() != points.Count)
                return;

            Vector horizontal = points[columns - 1] - points[0];
            Vector vertical = points[(rows - 1) * columns] - points[0];
            double cross = Vector.CrossProduct(horizontal, vertical);
            if (!double.IsFinite(cross) || Math.Abs(cross) <= 1e-6 * horizontal.Length * vertical.Length)
                return;

            // Legacy results have coordinates only. Validate their row-major progression before connecting them.
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (column + 1 < columns && Vector.Multiply(points[index + 1] - points[index], horizontal) <= 0)
                        return;
                    if (row + 1 < rows && Vector.Multiply(points[index + columns] - points[index], vertical) <= 0)
                        return;
                }
            }

            double zoom = AlgorithmResultOverlay.GetZoom(drawContext);
            Pen gridPen = new(Brushes.DeepSkyBlue, 1 / zoom);
            Pen referencePen = new(Brushes.OrangeRed, 2 / zoom);
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (column + 1 < columns)
                        AlgorithmResultOverlay.AddLine(drawContext, points[index], points[index + 1], gridPen, tag);
                    if (row + 1 < rows)
                        AlgorithmResultOverlay.AddLine(drawContext, points[index], points[index + columns], gridPen, tag);
                }
            }

            int[] corners = [0, columns - 1, rows * columns - 1, (rows - 1) * columns];
            for (int index = 0; index < corners.Length; index++)
                AlgorithmResultOverlay.AddLine(drawContext, points[corners[index]], points[corners[(index + 1) % corners.Length]], referencePen, tag);
        }
    }
}
