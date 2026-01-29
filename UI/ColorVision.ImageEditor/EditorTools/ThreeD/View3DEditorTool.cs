using ColorVision.Common.MVVM;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.ThreeD
{
    public record class View3DEditorTool(EditorContext EditorContext) : IEditorTool
    {
        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => "View3D";
        public int Order { get; set; } = 30;

        // Keep simple text like the original button
        public object Icon { get; set; } = "3D";

        public ICommand? Command { get; set; } = new RelayCommand(_ =>
        {
            if (EditorContext.DrawCanvas.Source is WriteableBitmap writeableBitmap)
            {
                var window3D = new Window3D(writeableBitmap)
                {
                    Owner = Application.Current.GetActiveWindow()
                };
                window3D.Show();
            }
        });
    }
}
