using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class ExportAnnotationsEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
        public string? GuidId => "ExportAnnotations";

        public int Order { get; set; } = 5;

        public object? Icon { get; set; } = MenuItemIcon.TryFindResource("DISave");

        public ICommand? Command { get; set; } = new RelayCommand(a =>
        {
            EditorContext.ExportAnnotations();
        }, a => true);
    }
}
