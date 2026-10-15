using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate;
using System.Windows;

namespace ProjectARVRPro.Process.Distortion
{
    internal static class DistortionResultOverlay
    {
        public static void AppendGrid(ImageView imageView, IReadOnlyList<Point> points, bool enabled)
        {
            var drawContext = imageView.EditorContext.DrawEditorContext;
            AlgorithmResultOverlay.ClearTagged(drawContext, AlgorithmResultOverlay.GridDistortionTag);
            if (enabled)
                GridDistortionOverlayRenderer.AppendLines(drawContext, points, 3, 3, AlgorithmResultOverlay.GridDistortionTag);
        }
    }
}
