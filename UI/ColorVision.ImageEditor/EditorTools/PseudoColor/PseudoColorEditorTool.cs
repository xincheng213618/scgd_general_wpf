#pragma warning disable CA1816
using ColorVision.ImageEditor.Abstractions;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorEditorTool : IEditorCustomControlTool, IDisposable
    {
        private readonly EditorContext _editorContext;
        private readonly PseudoColorController _controller;
        private readonly PseudoColorToolState _state;
        private PseudoColorToolControl? _toolControl;

        public PseudoColorEditorTool(EditorContext editorContext)
        {
            _editorContext = editorContext;
            _state = new PseudoColorToolState();
            _state.ApplyDefaults(PseudoColorDefaultConfig.Current);

            _controller = new PseudoColorController(editorContext.ProcessingContext, _state);
            _editorContext.RegisterService<IPseudoColorService>(_controller);
            _controller.RefreshPreview();
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => nameof(PseudoColorEditorTool);
        public int Order => 40;
        public object? Icon => null;
        public ICommand? Command => null;
        internal PseudoColorToolState State => _state;

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
            _editorContext.UnregisterService<IPseudoColorService>();
        }
    }
}
