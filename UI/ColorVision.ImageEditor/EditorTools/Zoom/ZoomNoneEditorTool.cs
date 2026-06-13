using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomNoneEditorTool(DrawEditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomNone";

        public int Order { get; set; } = 3;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImage1_1");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.Zoombox.ZoomNone();
        });
    }

}
