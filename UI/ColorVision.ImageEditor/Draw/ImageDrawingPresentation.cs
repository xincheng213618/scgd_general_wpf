using ColorVision.Common.Utilities;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Draw
{
    /// <summary>Owns visual presentation, collection synchronization and drawing display subscriptions for one view.</summary>
    internal sealed class ImageDrawingPresentation : IDisposable
    {
        private readonly DrawEditorContext _draw;
        private readonly ImageViewConfig _config;
        private readonly DrawCanvas _canvas;
        private readonly string _layoutDebounceKey;
        private double _oldZoomRatio;
        private bool _isUpdatedRender;
        private bool _attached;
        private bool _disposed;

        internal ImageDrawingPresentation(DrawEditorContext draw, ImageViewConfig config)
        {
            _draw = draw;
            _config = config;
            _canvas = draw.DrawCanvas;
            _layoutDebounceKey = "ImageLayoutUpdatedRender" + draw.Id;
        }

        // Components initialize first; the host attaches presentation subscriptions afterwards.
        internal void Attach()
        {
            if (_attached || _disposed) return;
            _attached = true;
            _canvas.VisualsAdd += OnVisualsAdded;
            _canvas.VisualsRemove += OnVisualsRemoved;
            _config.ShowTextChanged += OnShowTextChanged;
            _config.ShowMsgChanged += OnShowMsgChanged;
            _config.LayoutUpdatedChanged += OnLayoutUpdatedChanged;
            _config.DrawingTextFontSizeChanged += OnDrawingTextFontSizeChanged;
            _draw.Zoombox.ContentMatrixChanged += OnContentMatrixChanged;
            _draw.Zoombox.LayoutUpdated += OnLayoutUpdated;
            _canvas.IsLayoutUpdated = _config.IsLayoutUpdated;
            _canvas.TextFontSizeOverride = _config.DrawingTextFontSize;
            UpdateScale();
        }

        private void OnShowTextChanged(object? sender, bool value)
        {
            foreach (IDrawingVisual drawingVisual in _draw.DrawingVisualLists)
            {
                if (drawingVisual.BaseAttribute is ITextProperties textProperties)
                {
                    textProperties.IsShowText = _config.IsShowText;
                    drawingVisual.Render();
                }
            }
        }

        private void OnShowMsgChanged(object? sender, bool value)
        {
            foreach (IDrawingVisual drawingVisual in _draw.DrawingVisualLists)
            {
                if (drawingVisual is not DrawingVisualBase visual || visual.IsMessageVisible == value) continue;
                visual.IsMessageVisible = value;
                if (visual.BaseAttribute is BaseProperties attribute && !string.IsNullOrWhiteSpace(attribute.Msg)) visual.Render();
            }
        }

        private void OnLayoutUpdatedChanged(object? sender, bool value)
        {
            _canvas.IsLayoutUpdated = value;
            UpdateScale();
            _canvas.ApplyLayoutScaleToVisuals();
        }

        private void OnDrawingTextFontSizeChanged(object? sender, double value)
        {
            _canvas.TextFontSizeOverride = value;
            _canvas.ApplyLayoutScaleToVisuals();
        }

        private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateScale();

        private void OnContentMatrixChanged(object? sender, EventArgs e)
        {
            UpdateScale();
            double zoomRatio = _draw.ZoomRatio;
            if (_oldZoomRatio == zoomRatio) return;
            _oldZoomRatio = zoomRatio;
            double scale = GetScale();
            _canvas.Scale = scale;
            if (_config.IsLayoutUpdated)
                DebounceTimer.AddOrResetTimerDispatcher(_layoutDebounceKey, 20, () => RenderLayoutScale(scale));
        }

        private void RenderLayoutScale(double scale)
        {
            if (_disposed || _isUpdatedRender) return;
            try
            {
                _isUpdatedRender = true;
                _canvas.Scale = scale;
                _canvas.ApplyLayoutScaleToVisuals();
            }
            finally { _isUpdatedRender = false; }
        }

        internal void ZoomToFit()
        {
            if (_disposed) return;
            if (!_canvas.Dispatcher.CheckAccess())
            {
                _canvas.Dispatcher.BeginInvoke(ZoomToFit);
                return;
            }
            _draw.Zoombox.ZoomUniform();
            _canvas.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
            {
                if (_disposed) return;
                UpdateScale();
                _canvas.ApplyLayoutScaleToVisuals();
            }));
        }

        private void UpdateScale() => _canvas.Scale = GetScale();

        private double GetScale()
        {
            double zoomRatio = _draw.ZoomRatio;
            return double.IsNaN(zoomRatio) || double.IsInfinity(zoomRatio) || zoomRatio <= 0 ? 1 : 1 / zoomRatio;
        }

        private void OnVisualsAdded(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual)
            {
                ApplyDrawingVisualDisplayConfig(visual);
                _draw.DrawingVisualLists.Add(visual);
                return;
            }

            List<IDrawingVisual> drawingVisuals = e.Visuals.OfType<IDrawingVisual>().ToList();
            foreach (IDrawingVisual drawingVisual in drawingVisuals)
                ApplyDrawingVisualDisplayConfig(drawingVisual);
            _draw.AddDrawingVisuals(drawingVisuals);
        }

        private void ApplyDrawingVisualDisplayConfig(IDrawingVisual visual, bool renderChanges = true)
        {
            bool requiresRender = false;
            BaseProperties? baseAttribute = visual.BaseAttribute;
            if (baseAttribute is ITextProperties textProperties && textProperties.IsShowText != _config.IsShowText)
            {
                textProperties.IsShowText = _config.IsShowText;
                requiresRender = true;
            }

            if (visual is DrawingVisualBase drawingVisual && drawingVisual.IsMessageVisible != _config.IsShowMsg)
            {
                drawingVisual.IsMessageVisible = _config.IsShowMsg;
                requiresRender |= baseAttribute != null && !string.IsNullOrWhiteSpace(baseAttribute.Msg);
            }

            if (requiresRender && renderChanges)
                visual.Render();
        }

        private void OnVisualsRemoved(object? sender, VisualChangedEventArgs e)
        {
            if (e.Visual is IDrawingVisual visual)
                _draw.DrawingVisualLists.Remove(visual);
        }

        internal void AddVisual(Visual visual)
        {
            if (visual is IDrawingVisual drawingVisual
                && visual is DrawingVisual renderedVisual
                && renderedVisual.Drawing == null)
            {
                _canvas.TrySynchronizeDetachedVisualDpi(renderedVisual);
                ApplyDrawingVisualDisplayConfig(drawingVisual, renderChanges: false);
                if (visual is ILayoutScaleDrawingVisual scalableVisual)
                {
                    scalableVisual.ApplyLayoutScale(new DrawingVisualScaleContext(
                        _canvas.IsLayoutUpdated,
                        _canvas.Scale,
                        _canvas.TextFontSizeOverride));
                }

                if (renderedVisual.Drawing == null)
                    drawingVisual.Render();
            }

            _canvas.AddVisualCommand(visual);
        }

        internal void CommitActiveEdits()
        {
            List<IEditableDrawingVisual> activeEditors = _draw.DrawingVisualLists
                .OfType<IEditableDrawingVisual>()
                .Where(editor => editor.IsEditing)
                .ToList();
            if (activeEditors.Count == 0)
                return;

            foreach (IEditableDrawingVisual editor in activeEditors)
                editor.EndEdit(true);

            // Editing starts with an empty selection. Keep that state and avoid
            // exporting the selection handles restored by EndEdit.
            _draw.SelectionVisual.ClearRender();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            DebounceTimer.Cancel(_layoutDebounceKey);
            if (!_attached) return;
            _canvas.VisualsAdd -= OnVisualsAdded;
            _canvas.VisualsRemove -= OnVisualsRemoved;
            _config.ShowTextChanged -= OnShowTextChanged;
            _config.ShowMsgChanged -= OnShowMsgChanged;
            _config.LayoutUpdatedChanged -= OnLayoutUpdatedChanged;
            _config.DrawingTextFontSizeChanged -= OnDrawingTextFontSizeChanged;
            _draw.Zoombox.ContentMatrixChanged -= OnContentMatrixChanged;
            _draw.Zoombox.LayoutUpdated -= OnLayoutUpdated;
        }
    }
}
