using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ColorVision.ImageEditor.Realtime;

namespace ColorVision.ImageEditor.EditorTools.Realtime
{
    public sealed class FreezeRealtimeFrameEditorTool : IEditorToggleToolBase, IDisposable
    {
        private readonly EditorContext _editorContext;

        public FreezeRealtimeFrameEditorTool(EditorContext editorContext)
        {
            _editorContext = editorContext;
            ToolBarLocal = ToolBarLocal.Top;
            Order = 6;
            Icon = CreateRunPausedIcon();
            _editorContext.ImageView.Realtime.Options.PropertyChanged += Options_PropertyChanged;
        }

        public override string? GuidId => nameof(FreezeRealtimeFrameEditorTool);

        public override bool IsChecked
        {
            get => _editorContext.ImageView.Realtime.IsFrozen;
            set
            {
                if (_editorContext.ImageView.Realtime.IsFrozen == value) return;
                _editorContext.ImageView.Realtime.IsFrozen = value;
                OnPropertyChanged();
            }
        }

        private void Options_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RealtimeFrameOptions.IsFrozen))
            {
                OnPropertyChanged(nameof(IsChecked));
            }
        }

        private static FrameworkElement CreateRunPausedIcon()
        {
            Rectangle rectangle = new()
            {
                Width = 16,
                Height = 16,
                Stretch = Stretch.None,
            };
            rectangle.SetResourceReference(Shape.FillProperty, "DIRunPaused");
            return rectangle;
        }

        public void Dispose()
        {
            _editorContext.ImageView.Realtime.Options.PropertyChanged -= Options_PropertyChanged;
        }
    }
}
