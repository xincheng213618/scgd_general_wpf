using ColorVision.Common.MVVM;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.AppCommand
{
    public record class CloseImageEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Left;
        public string? GuidId => "CloseImageEditorTool";

        public int Order { get; set; } = 3;

        public object? Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageClear");
        public ICommand? Command { get; set; } = new RelayCommand(a =>
        {
            EditorContext.ImageView.Clear();
        }, a => true);
    }

}
