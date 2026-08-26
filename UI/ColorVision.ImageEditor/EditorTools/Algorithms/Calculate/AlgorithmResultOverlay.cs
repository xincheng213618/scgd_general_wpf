using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.Abstractions;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate
{
    /// <summary>
    /// Helpers for transient algorithm result overlays. Tagged overlays are intentionally
    /// kept out of the annotation/undo stream and can be replaced independently.
    /// </summary>
    internal static class AlgorithmResultOverlay
    {
        private sealed class RequestState
        {
            public ConcurrentDictionary<string, long> Versions { get; } = new(StringComparer.Ordinal);
        }

        private static readonly ConditionalWeakTable<DrawEditorContext, RequestState> RequestStates = new();

        public const string FindCrossTag = "ImageView.FindCrossLocal.Result";
        public const string FindLuminousAreaTag = "ImageView.FindLuminousArea.Result";

        public static void ClearTagged(DrawEditorContext drawContext, string tag)
        {
            DrawingVisualBase[] visuals = drawContext.DrawCanvas.Visuals
                .OfType<DrawingVisualBase>()
                .Where(visual => Equals(visual.BaseAttribute.Tag, tag))
                .ToArray();
            foreach (DrawingVisualBase visual in visuals)
            {
                drawContext.DrawCanvas.RemoveOverlayVisual(visual);
            }
        }

        public static long BeginRequest(DrawEditorContext drawContext, string tag) =>
            RequestStates.GetOrCreateValue(drawContext).Versions.AddOrUpdate(tag, 1, static (_, value) => value + 1);

        public static bool IsCurrentRequest(DrawEditorContext drawContext, string tag, long requestId) =>
            RequestStates.TryGetValue(drawContext, out RequestState? state) &&
            state.Versions.TryGetValue(tag, out long current) && current == requestId;

        public static void InvalidateRequest(DrawEditorContext drawContext, string tag) =>
            RequestStates.GetOrCreateValue(drawContext).Versions.AddOrUpdate(tag, 1, static (_, value) => value + 1);

        public static void AddPolygon(
            DrawEditorContext drawContext,
            IReadOnlyList<Point> points,
            Pen pen,
            string tag)
        {
            if (points.Count < 2) return;

            DVPolygon polygon = new() { IsComple = true };
            polygon.Attribute.Pen = pen;
            polygon.Attribute.Brush = Brushes.Transparent;
            polygon.Attribute.Points.AddRange(points);
            Add(drawContext, polygon, tag);
        }

        public static void AddLine(
            DrawEditorContext drawContext,
            Point start,
            Point end,
            Pen pen,
            string tag)
        {
            if (!IsFinite(start) || !IsFinite(end)) return;

            DVLine line = new(new LineProperties
            {
                Pen = pen,
                Points = new List<Point> { start, end }
            });
            Add(drawContext, line, tag);
        }

        public static void AddLabel(
            DrawEditorContext drawContext,
            Point center,
            string message,
            Brush brush,
            string tag)
        {
            if (!IsFinite(center) || string.IsNullOrWhiteSpace(message)) return;

            double zoom = GetZoom(drawContext);
            DVCircleText marker = new(new CircleTextProperties
            {
                Center = center,
                Radius = 6 / zoom,
                Brush = Brushes.Transparent,
                Pen = new Pen(brush, 1.5 / zoom),
                Foreground = brush,
                FontSize = 12 / zoom,
                Msg = message,
                Text = string.Empty
            });
            Add(drawContext, marker, tag);
        }

        public static double GetZoom(DrawEditorContext drawContext)
        {
            double zoom = drawContext.Zoombox.ContentMatrix.M11;
            return double.IsFinite(zoom) && zoom > 0 ? zoom : 1;
        }

        private static bool IsFinite(Point point) =>
            double.IsFinite(point.X) && double.IsFinite(point.Y);

        private static void Add(DrawEditorContext drawContext, DrawingVisualBase visual, string tag)
        {
            visual.BaseAttribute.Tag = tag;
            visual.Render();
            drawContext.DrawCanvas.AddOverlayVisual(visual);
        }
    }

    /// <summary>
    /// Clears transient algorithm evidence whenever ImageView switches source images.
    /// </summary>
    public sealed class AlgorithmResultOverlayLifecycle : IImageComponent
    {
        public void Execute(ImageView imageView)
        {
            imageView.ImageSourceLoaded += (_, _) =>
            {
                DrawEditorContext drawContext = imageView.EditorContext.DrawEditorContext;
                foreach (string tag in new[]
                {
                    AlgorithmResultOverlay.FindCrossTag,
                    AlgorithmResultOverlay.FindLuminousAreaTag
                })
                {
                    AlgorithmResultOverlay.InvalidateRequest(drawContext, tag);
                    AlgorithmResultOverlay.ClearTagged(drawContext, tag);
                }
            };
        }
    }
}
