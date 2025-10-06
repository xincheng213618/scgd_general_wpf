using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class OpenImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "OpenImage";

        public int Order { get; set; } = 1;

        public object? Icon { get; set; } = MenuItemIcon.TryFindResource("DIOpen");

        public ICommand? Command { get; set; } = ApplicationCommands.Open;
    }

}
