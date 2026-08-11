using System;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public sealed class VectorizedProperties : BaseProperties
    {
    }

    /// <summary>
    /// A selectable vector drawing with explicit image-coordinate bounds.
    /// </summary>
    public sealed class VectorizedSelectVisual : DrawingVisualBase<VectorizedProperties>, IDrawingVisual, ISelectVisual
    {
        private readonly Rect _sourceRect;

        public Drawing VectorDrawing { get; }

        public Rect Rect { get; private set; }

        public Pen Pen { get; set; } = new();

        public VectorizedSelectVisual(Drawing drawing, Rect rect)
        {
            ArgumentNullException.ThrowIfNull(drawing);
            if (rect.IsEmpty)
                throw new ArgumentException("Vector drawing bounds cannot be empty.", nameof(rect));

            VectorDrawing = drawing.IsFrozen ? drawing : drawing.CloneCurrentValue();
            if (VectorDrawing.CanFreeze && !VectorDrawing.IsFrozen)
                VectorDrawing.Freeze();

            Attribute = new VectorizedProperties();
            _sourceRect = rect;
            Rect = rect;
            Render();
        }

        public override void Render()
        {
            using DrawingContext drawingContext = RenderOpen();
            if (Rect == _sourceRect)
            {
                drawingContext.DrawDrawing(VectorDrawing);
                return;
            }

            Matrix transform = CreateBoundsTransform(_sourceRect, Rect);
            drawingContext.PushTransform(new MatrixTransform(transform));
            drawingContext.DrawDrawing(VectorDrawing);
            drawingContext.Pop();
        }

        public override Rect GetRect() => Rect;

        public override void SetRect(Rect rect)
        {
            if (rect.IsEmpty)
                return;

            Rect = rect;
            Render();
        }

        private static Matrix CreateBoundsTransform(Rect source, Rect target)
        {
            double scaleX = source.Width > 0 ? target.Width / source.Width : 1;
            double scaleY = source.Height > 0 ? target.Height / source.Height : 1;
            double offsetX = target.X - source.X * scaleX;
            double offsetY = target.Y - source.Y * scaleY;
            return new Matrix(scaleX, 0, 0, scaleY, offsetX, offsetY);
        }
    }
}
