using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomNoneEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomNone";

        public int Order { get; set; } = 3;

        public object Icon { get; set; } = IEditorToolFactory.GetImageFromResource("DrawingImageexpend");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.ZoomboxSub.ZoomNone();
        });
    }

}
