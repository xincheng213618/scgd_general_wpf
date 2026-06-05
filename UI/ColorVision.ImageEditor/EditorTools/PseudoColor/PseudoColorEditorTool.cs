using ColorVision.ImageEditor.Abstractions;
using ColorVision.ImageEditor.Settings;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.PseudoColor
{
    public class PseudoColorEditorTool : IEditorCustomControlTool, IDisposable, IImageViewSettingProvider, IImageViewSettingPersistence
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

        public FrameworkElement CreateToolControl()
        {
            _toolControl ??= new PseudoColorToolControl
            {
                DataContext = _state,
            };
            return _toolControl;
        }

        public IEnumerable<ImageViewSettingMetadata> GetImageViewSettings(ImageView imageView)
        {
            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.PseudoColor_Group,
                Order = 10,
                Scope = ImageViewSettingScope.CurrentView,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.PseudoColor_CurrentPseudoColor,
                Description = Properties.Resources.PseudoColor_CurrentDescription,
                Source = _state,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = Properties.Resources.PseudoColor_Group,
                Order = 20,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = Properties.Resources.PseudoColor_DefaultPseudoColor,
                Description = Properties.Resources.PseudoColor_DefaultDescription,
                Source = PseudoColorDefaultConfig.Current,
            };
        }

        public void SaveImageViewSettings(ImageView imageView)
        {
            PseudoColorDefaultConfig.SaveCurrent();
        }

        public void Dispose()
        {
            _controller.Dispose();
            _editorContext.UnregisterService<IPseudoColorService>();
        }
    }
}
