using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class PolygonManager : MultiPointDrawingToolBase<DVPolygon>
    {
        public PolygonManager(DrawEditorContext context) : base(context)
        {
            Order = 5;

            Icon = IEditorToolFactory.TryFindResource("DrawingImagePolygon");
        }

        public bool CloseOnComplete { get; set; } = true;

        protected override IEnumerable<CompactInspectorItem> BuildCompactInspectorItems()
        {
            yield return new CompactInspectorPropertyItem { Source = this, PropertyName = nameof(CloseOnComplete), Label = "闭合", ShowLabel = true, Order = 0, EditorKind = CompactInspectorEditorKind.Toggle, ToolTip = "完成时闭合区域；Enter 完成，Esc 取消" };
            foreach (var item in base.BuildCompactInspectorItems()) yield return item;
        }

        protected override bool CompletesAtPoint(DVPolygon visual, Point point)
        {
            bool closes = visual.Points.Count >= 4 && (visual.Points[0] - point).Length <= 8 / GetSafeZoomRatio();
            if (closes) CloseOnComplete = true;
            return closes;
        }

        protected override bool SupportsKeyboardCompletion => true;
        protected override bool SelectOnMouseUp => true;

        protected override DVPolygon CreateVisual()
        {
            return new DVPolygon();
        }

        protected override IList<Point> GetPoints(DVPolygon visual)
        {
            return visual.Points;
        }

        protected override void RenderVisual(DVPolygon visual)
        {
            visual.Render();
        }

        protected override void OnVisualCreated(DVPolygon visual)
        {
            visual.Attribute.Id = EditorContext.DrawingVisualLists.Count + 1;
            double zoomRatio = GetSafeZoomRatio();
            visual.Attribute.Brush = StyleConfig.StrokeBrush;
            visual.Attribute.Pen = new Pen(StyleConfig.StrokeBrush, StyleConfig.StrokeThickness / zoomRatio);
        }

        protected override void OnVisualCompleted(DVPolygon visual)
        {
            if (visual.Points.Count < (CloseOnComplete ? 3 : 2))
            {
                CancelActiveVisual();
                return;
            }

            visual.IsComple = CloseOnComplete;
            visual.Render();
            base.OnVisualCompleted(visual);
        }
    }
}
