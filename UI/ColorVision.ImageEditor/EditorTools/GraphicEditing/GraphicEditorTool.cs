using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.GraphicEditing
{
    public record class GraphicEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Draw;
        public string? GuidId => "GraphicEditorTool";

        public int Order { get; set; } = 999;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DIExpand");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {

            GraphicEditingWindow graphicEditingWindow = new GraphicEditingWindow(EditorContext) { Owner = Application.Current.GetActiveWindow() };

            // 屏幕坐标
            var point = EditorContext.DrawCanvas.PointToScreen(new Point(EditorContext.DrawCanvas.ActualWidth, EditorContext.DrawCanvas.ActualHeight));

            // 转换为WPF坐标
            var source = PresentationSource.FromVisual(EditorContext.DrawCanvas);
            if (source != null)
            {
                var targetPoint = source.CompositionTarget.TransformFromDevice.Transform(point);

                // 设置弹窗的位置
                graphicEditingWindow.WindowStartupLocation = WindowStartupLocation.Manual;
                graphicEditingWindow.Left = targetPoint.X - graphicEditingWindow.Width;
                graphicEditingWindow.Top = targetPoint.Y - graphicEditingWindow.Height;
            }
            graphicEditingWindow.Show();


        });
    }
}
