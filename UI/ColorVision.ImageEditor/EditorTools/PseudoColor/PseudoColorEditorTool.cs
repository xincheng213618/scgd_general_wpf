#pragma warning disable CA1816
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorEditorTool : IEditorCustomControlTool, IDisposable
    {
        private readonly PseudoColorController _controller;
        private readonly PseudoColorToolState _state;
        private PseudoColorToolControl? _toolControl;

        public PseudoColorEditorTool(EditorContext editorContext)
        {
            _state = new PseudoColorToolState();
            _state.ApplyDefaults(PseudoColorDefaultConfig.Current);

            _controller = new PseudoColorController(editorContext.ProcessingContext, _state);
            _controller.RefreshPreview();
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => nameof(PseudoColorEditorTool);
        public int Order => 40;
        public object? Icon => null;
        public ICommand? Command => null;
        internal PseudoColorToolState State => _state;

        public void ConfigureForImage() => _controller.ConfigureForImage();
        public void Invalidate() => _controller.Invalidate();
        public void Reset() => _controller.Reset();

        public FrameworkElement CreateToolControl()
        {
            _toolControl ??= new PseudoColorToolControl
            {
                DataContext = _state,
            };
            return _toolControl;
        }

        public void Dispose()
        {
            _controller.Dispose();
        }
    }
}
