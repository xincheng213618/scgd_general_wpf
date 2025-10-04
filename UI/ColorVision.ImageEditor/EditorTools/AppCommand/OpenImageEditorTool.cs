using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class OpenImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "OpenImage";

        public int Order { get; set; } = 1;

        public object Icon { get; set; } = new Image() { Source = (DrawingImage)Application.Current.FindResource("openDrawingImage") };

        public ICommand? Command { get; set; } = ApplicationCommands.Open;
    }


}
