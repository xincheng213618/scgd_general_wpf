using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.WindowTools
{
    public class FullScreenEditorTool : IEditorTool
    {
        public FullScreenEditorTool(EditorContext context)
        {
            Command = new RelayCommand(_ => context.ImageView.ToggleFullScreen());
        }
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "FullScreen";
        public int Order { get; set; } = 50;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageMax");

        public ICommand? Command { get; set; } 
    }
}
