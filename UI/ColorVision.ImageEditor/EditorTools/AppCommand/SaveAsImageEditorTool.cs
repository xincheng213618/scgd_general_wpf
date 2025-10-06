using ColorVision.UI.Menus;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class SaveAsImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
        public string? GuidId => "SaveAs";

        public int Order { get; set; } = 2;

        public object? Icon { get; set; } = MenuItemIcon.TryFindResource("DISave");

        public ICommand? Command { get; set; } = ApplicationCommands.SaveAs;
    }


}
