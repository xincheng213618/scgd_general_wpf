using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class OpenImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
        public string? GuidId => "OpenImage";

        public int Order { get; set; } = 1;

        public object? Icon { get; set; } = IEditorToolFactory.TryFindResource("openDrawingImage");

        public ICommand? Command { get; set; } = new RelayCommand(
            _ => ApplicationCommands.Open.Execute(null, EditorContext.ImageView),
            _ => ApplicationCommands.Open.CanExecute(null, EditorContext.ImageView));
    }

}
