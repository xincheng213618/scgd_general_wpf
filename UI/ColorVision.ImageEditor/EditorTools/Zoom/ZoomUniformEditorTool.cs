using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomUniformEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomUniform";

        public int Order { get; set; }

        public object Icon { get; set; } = IEditorToolFactory.GetImageFromResource("DrawingImage1_1");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.ZoomboxSub.ZoomUniform();
        });
    }

}
