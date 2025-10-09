using ColorVision.Common.MVVM;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools
{
    public class ZoomRatioEditorTool : ViewModelBase, IEditorTextTool
    {
        public EditorContext EditorContext { get; set; }
        public ZoomRatioEditorTool(EditorContext context)
        {
            EditorContext = context;
            EditorContext.Zoombox.ContentMatrixChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(ZoomRatio));
            };
        }

        public double ZoomRatio { get => EditorContext.Zoombox.ContentMatrix.M11; set { EditorContext.Zoombox.Zoom(value/ EditorContext.Zoombox.ContentMatrix.M11); } }

        public Binding Binding { get; set; } = new Binding(nameof(ZoomRatio))
        {
            Mode = BindingMode.TwoWay,
            StringFormat = "F2",
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        };


        public ToolBarLocal ToolBarLocal => ToolBarLocal.Top;
        public string? GuidId => "ZoomRatio";

        public int Order { get; set; } = 10;

        public object Icon { get; set; }

        public ICommand? Command { get; set; }
    }

}
