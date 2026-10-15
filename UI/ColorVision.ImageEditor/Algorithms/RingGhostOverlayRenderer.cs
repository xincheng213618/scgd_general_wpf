using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms;

/// <summary>Replays saved pixel geometry through the neutral overlay lifecycle.</summary>
public static class RingGhostOverlayRenderer
{
    private sealed class Session { public IDisposable? Registration; }
    private static readonly ConditionalWeakTable<ImageView, Session> Sessions = new();

    public static void Render(ImageView view, int width, int height, IReadOnlyList<AlgorithmGeometry> regions)
    {
        ArgumentNullException.ThrowIfNull(view);
        Session session = Sessions.GetOrCreateValue(view);
        session.Registration?.Dispose();
        session.Registration = null;
        if (regions.Count == 0) return;
        if (view.ViewBitmapSource is not BitmapSource bitmap || bitmap.PixelWidth != width || bitmap.PixelHeight != height) return;
        using AlgorithmResult result = new()
        {
            Status = AlgorithmResultStatus.Succeeded,
            Artifacts =
            [
                new AlgorithmGeometryArtifact("ring-ghost-regions", AlgorithmCoordinateSpace.Pixel, regions),
                new AlgorithmOverlayArtifact("ring-ghost-result", AlgorithmOverlayLifetime.Transient,
                    regions.Select((region, index) => new AlgorithmOverlayItem(region.Id, new AlgorithmOverlayStyle(
                        Stroke: index < 2 ? "#FF7BF15C" : index < 4 ? "#FFFFBA38" : "#FF4DE3FF", Label: region.Id))).ToArray())
            ]
        };
        session.Registration = AlgorithmOverlayRenderer.Apply(view.EditorContext.ProcessingContext, view.EditorContext.DrawEditorContext, result);
    }
}
