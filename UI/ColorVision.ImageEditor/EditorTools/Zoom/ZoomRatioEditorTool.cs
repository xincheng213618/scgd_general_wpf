using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw;
using System;
using System.Windows.Data;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools
{
    public class ZoomRatioEditorTool : ViewModelBase, IEditorTextTool, IDisposable
    {
        private readonly Zoombox _zoombox;
        private double _zoomRatio;

        public DrawEditorContext EditorContext { get; set; }

        public ZoomRatioEditorTool(DrawEditorContext context)
        {
            EditorContext = context;
            _zoombox = context.Zoombox;
            _zoomRatio = ZoomRatio;
            _zoombox.ContentMatrixChanged += OnContentMatrixChanged;
        }

        private void OnContentMatrixChanged(object? sender, EventArgs e)
        {
            double zoomRatio = ZoomRatio;
            if (_zoomRatio == zoomRatio) return;
            _zoomRatio = zoomRatio;
            OnPropertyChanged(nameof(ZoomRatio));
        }

        public void Dispose()
        {
            _zoombox.ContentMatrixChanged -= OnContentMatrixChanged;
            GC.SuppressFinalize(this);
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
