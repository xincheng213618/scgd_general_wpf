using ColorVision.UI;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class SaveAsImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "SaveAs";

        public int Order { get; set; } = 1;

        public object Icon { get; set; } = new Image() { Source = (DrawingImage)Application.Current.FindResource("DrawingImageSave") };

        public ICommand? Command { get; set; } = ApplicationCommands.SaveAs;
    }


}
