using ColorVision.Engine.Templates.Jsons.MTF2;
using ColorVision.ImageEditor.Draw;
using System.Windows.Media;

namespace ProjectARVRPro.Process
{
    public static class ProcessExtensions
    {
        internal static bool TryCreateOverlayPoint(double x, double y, out System.Windows.Point point)
        {
            if (!double.IsFinite(x) || !double.IsFinite(y))
            {
                point = default;
                return false;
            }

            point = new System.Windows.Point(x, y);
            return true;
        }

        internal static bool TryCreateOverlayRect(double x, double y, double width, double height, out System.Windows.Rect rect)
        {
            if (!double.IsFinite(x)
                || !double.IsFinite(y)
                || !double.IsFinite(width)
                || !double.IsFinite(height)
                || width <= 0
                || height <= 0
                || !double.IsFinite(x + width)
                || !double.IsFinite(y + height))
            {
                rect = System.Windows.Rect.Empty;
                return false;
            }

            rect = new System.Windows.Rect(x, y, width, height);
            return true;
        }

        internal static bool TryCreateMtfOverlay(
            MTFItem item,
            int id,
            string numberFormat,
            out DVRectangleText overlay)
        {
            ArgumentNullException.ThrowIfNull(item);
            if (!TryCreateOverlayRect(item.x, item.y, item.w, item.h, out System.Windows.Rect rect))
            {
                overlay = null!;
                return false;
            }

            overlay = new DVRectangleText(new RectangleTextProperties
            {
                Rect = rect,
                Brush = Brushes.Transparent,
                Pen = new Pen(Brushes.Red, 1),
                Id = id,
                Msg = item.mtfValue?.ToString(numberFormat),
            });
            return true;
        }
    }
}
