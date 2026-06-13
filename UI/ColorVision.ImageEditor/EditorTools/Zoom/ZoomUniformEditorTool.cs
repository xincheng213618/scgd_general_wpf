using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Zoom
{
    public record class ZoomUniformEditorTool(DrawEditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomUniform";

        public int Order { get; set; } = 3;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageexpend");

        public ICommand? Command { get; set; } = new RelayCommand(_ => EditorContext.Zoombox.ZoomUniform());
    }
}
