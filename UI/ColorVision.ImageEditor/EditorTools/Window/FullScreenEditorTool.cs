using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.WindowTools
{
    public record class FullScreenEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "FullScreen";
        public int Order { get; set; } = 50;

        public object Icon { get; set; } = new Image()
        {
            Source = (DrawingImage)Application.Current.FindResource("DrawingImageMax")
        };

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            EditorContext.ImageViewModel.MaxImage();
        });
    }
}
