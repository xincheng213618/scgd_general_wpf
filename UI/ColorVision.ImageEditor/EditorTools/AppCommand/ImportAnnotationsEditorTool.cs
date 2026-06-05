using ColorVision.Common.MVVM;
using ColorVision.UI.Menus;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class ImportAnnotationsEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
        public string? GuidId => "ImportAnnotations";

        public int Order { get; set; } = 4;

        public object? Icon { get; set; } = MenuItemIcon.TryFindResource("DIOpen");

        public ICommand? Command { get; set; } = new RelayCommand(a =>
        {
            EditorContext.ImportAnnotations();
        }, a => true);
    }
}
