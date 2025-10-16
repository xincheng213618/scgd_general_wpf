using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.EditorTools.FullScreen;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.WindowTools
{
    public class FullScreenEditorTool : IEditorTool
    {
        public FullScreenEditorTool(EditorContext EditorContext)
        {
            ImageFullScreenMode = new ImageFullScreenMode(EditorContext.ImageView);
            Command = new RelayCommand(_ =>
            {
                ImageFullScreenMode.ToggleFullScreen();
            });
        }
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "FullScreen";
        public int Order { get; set; } = 50;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageMax");
        public ImageFullScreenMode ImageFullScreenMode { get; set; } 

        public ICommand? Command { get; set; } 
    }
}
