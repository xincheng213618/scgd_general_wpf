using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomUniformToFillEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomUniformToFill";

        public int Order { get; set; } = 4;

        public object Icon { get; set; } = IEditorToolFactory.GetImageFromResource("DrawingImageexpend");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.ZoomboxSub.ZoomUniformToFill();
        });
    }

}
