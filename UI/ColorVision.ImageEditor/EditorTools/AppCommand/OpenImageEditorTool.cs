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

        public ICommand? Command { get; set; } = new RelayCommand(a =>
        {
            using var openFileDialog = new System.Windows.Forms.OpenFileDialog();
            openFileDialog.RestoreDirectory = true;
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                EditorContext.ImageView?.OpenImage(openFileDialog.FileName);
            }
        }, a => true);
    }

}
