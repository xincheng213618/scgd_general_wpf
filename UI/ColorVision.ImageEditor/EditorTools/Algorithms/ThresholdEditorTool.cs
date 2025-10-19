using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Algorithms
{
    public record class ThresholdEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "Threshold";
        public int Order { get; set; } = 45;

        public object Icon { get; set; } = IEditorToolFactory.TryFindResource("DrawingImageMax");

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            var imageView = EditorContext.ImageView;
            if (imageView == null) return;

            var window = new ThresholdWindow(imageView)
            {
                Owner = Application.Current.GetActiveWindow()
            };
            window.ShowDialog();
        });
    }
}
