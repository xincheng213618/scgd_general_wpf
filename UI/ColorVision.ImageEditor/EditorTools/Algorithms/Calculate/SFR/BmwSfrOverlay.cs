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
    internal static DrawingVisual CreateVisual(BmwTargetAnalysis target, ImageSelectionScope scope, double zoom, string channel, BmwSfrOverlaySettings? settings = null)
        => BmwSfrOverlayRenderer.CreateVisual(target, scope, zoom, channel, settings ?? new());
    internal static IAlgorithmOverlayRegistration? Apply(ImageProcessingContext image, BmwTargetAnalysis target, ImageSelectionScope scope, double zoom, string channel, BmwSfrOverlaySettings? settings = null)
    {
        var items = new List<AlgorithmOverlayItem>();
        foreach (var edge in target.Edges)
            items.Add(new($"{target.Id}/{edge.Id}",new(Label:BmwSfrPresentation.OverlayLabel(edge,channel,settings,target))));
        var artifact = new AlgorithmOverlayArtifact($"bmw-sfr/{target.Id}",AlgorithmOverlayLifetime.Transient,items);
        image.TryRegisterAlgorithmOverlay(artifact,CreateVisual(target,scope,zoom,channel,settings),scope.DocumentInstanceId,scope.SourceRevision,out var registration);
        return registration;
    }
}
