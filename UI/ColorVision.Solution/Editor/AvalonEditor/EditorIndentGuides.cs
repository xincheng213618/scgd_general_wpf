using ICSharpCode.AvalonEdit.Rendering;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Solution.Editor.AvalonEditor;

internal sealed class EditorIndentGuides : IBackgroundRenderer
{
    public KnownLayer Layer => KnownLayer.Background;
    public bool IsEnabled { get; set; } = true;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (!IsEnabled || !textView.VisualLinesValid || textView.Document == null) return;
        var pen = new Pen(textView.TryFindResource("EditorIndentGuideBrush") as Brush ?? Brushes.LightGray, 1);
        double columnWidth = textView.WideSpaceWidth;
        int tabSize = textView.Options.IndentationSize;
        foreach (var visualLine in textView.VisualLines)
        {
            var line = visualLine.FirstDocumentLine;
            int columns = 0;
            for (int i = 0; i < line.Length; i++)
            {
                char c = textView.Document.GetCharAt(line.Offset + i);
                if (c == ' ') columns++;
                else if (c == '\t') columns += tabSize - columns % tabSize;
                else break;
            }
            double top = visualLine.VisualTop - textView.VerticalOffset;
            for (int column = tabSize; column <= columns; column += tabSize)
            {
                double x = column * columnWidth - textView.HorizontalOffset;
                if (x < 0 || x > textView.ActualWidth) continue;
                drawingContext.DrawLine(pen, new Point(Math.Floor(x) + .5, top), new Point(Math.Floor(x) + .5, top + visualLine.Height));
            }
        }
    }
}
