using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomNoneEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomNone";

        public int Order { get; set; } = 3;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageexpend");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.Zoombox.ZoomNone();
        });
    }

}
