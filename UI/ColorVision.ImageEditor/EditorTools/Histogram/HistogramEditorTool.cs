using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.UI;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Histogram
{
    public record class HistogramEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "Histogram";
        public int Order { get; set; } = 10;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DIHistogram");

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            if (EditorContext.DrawCanvas.Source is not BitmapSource bitmapSource) return;

            var (redHistogram, greenHistogram, blueHistogram) = ImageUtils.RenderHistogram(bitmapSource);
            HistogramChartWindow window;
            
            // Detect single-channel vs multi-channel
            if (bitmapSource.Format.Masks.Count == 1)
            {
                // Single-channel (grayscale)
                window = new HistogramChartWindow(redHistogram);
            }
            else
            {
                // Multi-channel (RGB)
                window = new HistogramChartWindow(redHistogram, greenHistogram, blueHistogram);
            }

            window.Owner = Application.Current.GetActiveWindow();
            window.Show();
        });
    }
}
