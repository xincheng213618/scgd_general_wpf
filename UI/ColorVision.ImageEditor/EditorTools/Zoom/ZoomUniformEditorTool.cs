using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomUniformEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomUniform";

        public int Order { get; set; } = 3;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImage1_1");

        public ICommand? Command { get; set; } = new RelayCommand((o) =>
        {
            EditorContext.Zoombox.ZoomUniform();
        });
    }

}
