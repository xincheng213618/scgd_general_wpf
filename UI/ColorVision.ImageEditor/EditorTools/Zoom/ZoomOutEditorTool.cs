using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomOutEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomOut";

        public int Order { get; set; } = 2;

        public object Icon { get; set; } = new Image() { Source = (DrawingImage)Application.Current.FindResource("DrawingImagezoom_out") };

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.ZoomboxSub.Zoom(0.8);
        });
    }

}
