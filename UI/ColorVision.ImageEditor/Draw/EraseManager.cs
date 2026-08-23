#pragma warning disable CS0414,CS8625
using ColorVision.Common.MVVM;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class EraseManager : RegionOperationToolBase
    {
        private static readonly SolidColorBrush SelectionFill = new(Color.FromArgb(0x77, 0xF3, 0xF3, 0xF3));
        private static readonly Pen SelectionBorder = new(Brushes.Blue, 1);

        static EraseManager()
        {
            SelectionFill.Freeze();
            SelectionBorder.Freeze();
        }

        public EraseManager(DrawEditorContext context) : base(context)
        {
            Order = 2;
            Icon = IEditorToolFactory.TryFindResource("DrawingImageeraser");
        }

        protected override Cursor ActiveCursor => Input.Cursors.Eraser;
        protected override Cursor InactiveCursor => Cursors.Cross;

        public Func<Visual, bool>? CanEraseVisual { get; set; }

        DrawingVisual EraseVisual { get; set; }

        protected override void LoadCore()
        {
            EraseVisual = new DrawingVisual();
            DrawCanvas.MouseMove += MouseMove;
            DrawCanvas.PreviewMouseLeftButtonDown += PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp += Image_PreviewMouseUp;
        }

        protected override void UnLoadCore()
        {
            DrawCanvas.MouseMove -= MouseMove;
            DrawCanvas.PreviewMouseLeftButtonDown -= PreviewMouseLeftButtonDown;
            DrawCanvas.PreviewMouseUp -= Image_PreviewMouseUp;
            if (IsMouseDown)
                DrawCanvas.ReleaseMouseCapture();
            DrawCanvas.RemoveOverlayVisual(EraseVisual);
            IsMouseDown = false;
            EraseVisual = null;
        }

        Point MouseDownP { get; set; }
        Point MouseUpP { get; set; }

        bool IsMouseDown;
        public void DrawSelectRect(Rect rect)
        {
            using DrawingContext dc = EraseVisual.RenderOpen();
            dc.DrawRectangle(SelectionFill, SelectionBorder, rect);
        }
        private void PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DrawCanvas.CaptureMouse();
            BeginErase(e.GetPosition(DrawCanvas));
            e.Handled = true;
        }


        private void Image_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!IsMouseDown || e.ChangedButton != MouseButton.Left)
                return;

            DrawCanvas.ReleaseMouseCapture();
            CompleteErase(e.GetPosition(DrawCanvas));
            e.Handled = true;
        }

        private void BeginErase(Point point)
        {
            MouseDownP = point;
            IsMouseDown = true;
            DrawSelectRect(new Rect(point, point));
            DrawCanvas.AddOverlayVisual(EraseVisual);
            EditorContext.SelectionVisual.ClearRender();
        }

        private void CompleteErase(Point point)
        {
            MouseUpP = point;
            IsMouseDown = false;

            // The marquee is transient UI. Removing it before hit testing also keeps it
            // from obscuring the visual under either endpoint.
            DrawCanvas.RemoveOverlayVisual(EraseVisual);

            HashSet<Visual> eraseCandidates = new();
            AddEraseCandidate(eraseCandidates, DrawCanvas.GetVisual<Visual>(MouseDownP));
            AddEraseCandidate(eraseCandidates, DrawCanvas.GetVisual<Visual>(MouseUpP));
            foreach (DrawingVisual visual in DrawCanvas.GetVisuals(new RectangleGeometry(new Rect(MouseDownP, MouseUpP))))
                AddEraseCandidate(eraseCandidates, visual);

            RemoveVisualsAsSingleCommand(eraseCandidates);
        }

        private void AddEraseCandidate(HashSet<Visual> candidates, Visual? visual)
        {
            if (visual == null || ReferenceEquals(visual, EraseVisual) || candidates.Contains(visual))
                return;

            if (CanEraseVisual?.Invoke(visual) == false)
                return;

            candidates.Add(visual);
        }

        private void RemoveVisualsAsSingleCommand(HashSet<Visual> candidates)
        {
            List<(Visual Visual, int Index)> targets = new(candidates.Count);
            for (int index = 0; index < DrawCanvas.Visuals.Count; index++)
            {
                Visual visual = DrawCanvas.Visuals[index];
                if (candidates.Contains(visual))
                    targets.Add((visual, index));
            }
            if (targets.Count == 0)
                return;

            foreach ((Visual visual, _) in targets)
                DrawCanvas.RemoveVisual(visual);

            DrawCanvas.AddActionCommand(new ActionCommand(
                () =>
                {
                    foreach ((Visual visual, int index) in targets)
                        DrawCanvas.InsertVisual(index, visual);
                },
                () =>
                {
                    foreach ((Visual visual, _) in targets)
                        DrawCanvas.RemoveVisual(visual);
                })
            {
                Header = "移除",
            });
        }


        private void MouseMove(object sender, MouseEventArgs e)
        {
            if (!IsMouseDown)
                return;

            if (EraseVisual != null)
            {
                var point = e.GetPosition(DrawCanvas);
                DrawSelectRect(new Rect(MouseDownP, point));
            }
            e.Handled = true;
        }

    }
}
