using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

internal static class BmwSfrOverlay
{
    internal static DrawingVisual CreateVisual(BmwTargetAnalysis target, ImageSelectionScope scope, double zoom)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();
        double scale = double.IsFinite(zoom) && zoom > 0 ? zoom : 1;
        Point Position(double x, double y) => new(x * 96 / scope.DpiX, y * 96 / scope.DpiY);
        void Label(string text, Point anchor, Brush brush)
        {
            var formatted = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 12 / scale, brush, 1);
            anchor.X = Math.Clamp(anchor.X,0,Math.Max(0,scope.CanvasWidth-formatted.Width-8/scale));
            anchor.Y = Math.Clamp(anchor.Y,0,Math.Max(0,scope.CanvasHeight-formatted.Height-4/scale));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(220,20,24,30)),null,
                new Rect(anchor.X-3/scale,anchor.Y-2/scale,formatted.Width+6/scale,formatted.Height+4/scale),3/scale,3/scale);
            dc.DrawText(formatted,anchor);
        }
        var search = target.SearchRoi;
        Label($"{target.Id} · {(target.Located ? "MTF50 (cy/px)" : "INVALID · "+BmwSfrPresentation.Reason(target.Reason))}",
            Position(search.X,search.Y),Brushes.White);
        foreach (var edge in target.Edges)
        {
            var r = edge.Roi;
            if (r.Width <= 0 || r.Height <= 0) continue;
            bool valid = BmwSfrPresentation.Channel(edge,"L") is { Valid: true };
            Brush brush = valid ? Brushes.LightGreen : Brushes.Orange;
            dc.DrawRectangle(null,new Pen(brush,1.5/scale),new Rect(Position(r.X,r.Y),Position(r.X+r.Width,r.Y+r.Height)));
            Point anchor = edge.Id switch
            {
                BmwEdgeId.Left => Position(search.X, r.Y) + new Vector(-145 / scale,0),
                BmwEdgeId.Right => Position(search.X + search.Width,r.Y) + new Vector(6 / scale,0),
                BmwEdgeId.Top => Position(r.X,search.Y) + new Vector(0,-20 / scale),
                _ => Position(r.X,search.Y + search.Height) + new Vector(0,5 / scale)
            };
            Label(BmwSfrPresentation.OverlayLabel(edge),anchor,brush);

        }
        return visual;
    }

    internal static IAlgorithmOverlayRegistration? Apply(ImageProcessingContext image, BmwTargetAnalysis target, ImageSelectionScope scope, double zoom)
    {
        var items = new List<AlgorithmOverlayItem>();
        foreach (var edge in target.Edges)
            items.Add(new($"{target.Id}/{edge.Id}",new(Label:BmwSfrPresentation.OverlayLabel(edge))));
        var artifact = new AlgorithmOverlayArtifact($"bmw-sfr/{target.Id}",AlgorithmOverlayLifetime.Transient,items);
        image.TryRegisterAlgorithmOverlay(artifact,CreateVisual(target,scope,zoom),scope.DocumentInstanceId,scope.SourceRevision,out var registration);
        return registration;
    }
}
