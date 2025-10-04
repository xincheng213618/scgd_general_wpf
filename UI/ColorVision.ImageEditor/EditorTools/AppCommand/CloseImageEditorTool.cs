using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class CloseImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "CloseImageEditorTool";

        public int Order { get; set; } = 1;

        public object? Icon { get; set; } = MenuItemIcon.TryFindResource("DrawingImageClear");
        public ICommand? Command { get; set; } = ApplicationCommands.Close;
    }

}
