using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public record class WhiteBalanceEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "WhiteBalance";
        public int Order { get; set; } = 50;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageMax");

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            var imageView = EditorContext.ImageView;
            if (imageView == null) return;

            var window = new WhiteBalanceWindow(imageView)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.ShowDialog();
        });
    }
}
