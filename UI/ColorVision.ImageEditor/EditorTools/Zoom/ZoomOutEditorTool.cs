using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomOutEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomOut";

        public int Order { get; set; } = 2;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImagezoom_out");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.Zoombox.Zoom(0.8);
        });
    }

}
