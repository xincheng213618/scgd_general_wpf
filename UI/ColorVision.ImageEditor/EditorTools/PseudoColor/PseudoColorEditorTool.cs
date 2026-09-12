#pragma warning disable CA1816
using System;
using System.Windows;
using System.Windows.Input;
using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Presentation;
using ColorVision.ImageEditor.Presentation.PseudoColor;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorEditorTool : IEditorCustomControlTool, IDisposable
    {
        private readonly ImageDisplayEffects _effects;
        private PseudoColorToolControl? _toolControl;

        public PseudoColorEditorTool(EditorContext editorContext)
        {
            _effects = editorContext.ProcessingContext.DisplayEffects;
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;
        public string? GuidId => nameof(PseudoColorEditorTool);
        public int Order => 40;
        public object? Icon => null;
        public ICommand? Command => null;
        internal PseudoColorState State => _effects.PseudoColor;

        public void ConfigureForImage() => _effects.ConfigureForImage();
        public void Invalidate() => _effects.Invalidate();
        public void Reset() => _effects.ResetForSourceChange();

        public FrameworkElement CreateToolControl()
        {
            _toolControl ??= new PseudoColorToolControl
            {
                DataContext = State,
            };
            return _toolControl;
        }

        public void Dispose()
        {
            if (_toolControl != null) _toolControl.DataContext = null;
            _toolControl = null;
        }
    }
}
