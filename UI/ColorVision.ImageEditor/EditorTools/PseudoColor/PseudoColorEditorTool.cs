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

            _controller = new PseudoColorController(editorContext.ImageView, _state);
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
                Group = "伪彩色",
                Order = 10,
                Scope = ImageViewSettingScope.CurrentView,
                Type = ImageViewSettingType.Class,
                Name = "当前伪彩色",
                Description = "控制当前画布的伪彩色启用状态、当前色表和当前范围。这里的修改立即生效，不会写回默认值。",
                Source = _state,
            };

            yield return new ImageViewSettingMetadata
            {
                Group = "伪彩色",
                Order = 20,
                Scope = ImageViewSettingScope.GlobalDefault,
                Type = ImageViewSettingType.Class,
                Name = "伪彩色默认值",
                Description = "控制新图像或重置后采用的默认伪彩色类型和自动范围开关，不会直接覆盖当前画布状态。",
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