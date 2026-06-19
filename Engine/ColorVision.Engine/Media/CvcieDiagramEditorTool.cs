using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw.Special;
using cvColorVision;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Media
{
    internal sealed class CvcieDiagramEditorTool : IEditorTool, IDisposable
    {
        private readonly EditorContext _context;
        private readonly Func<IntPtr> _getConvertHandle;
        private readonly Action _ensureBufferLoaded;
        private readonly Func<CvcieMouseProbeOptions> _getProbeSettings;
        private WindowCIE? _windowCie;
        private EventHandler<ImagePixelSample>? _pixelSampleChangedHandler;

        public CvcieDiagramEditorTool(
            EditorContext context,
            Func<IntPtr> getConvertHandle,
            Action ensureBufferLoaded,
            Func<CvcieMouseProbeOptions> getProbeSettings)
        {
            _context = context;
            _getConvertHandle = getConvertHandle;
            _ensureBufferLoaded = ensureBufferLoaded;
            _getProbeSettings = getProbeSettings;
            Command = new RelayCommand(_ => OpenCieDiagram());
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;

        public string? GuidId => "CIE1931";

        public int Order => 0;

        public object? Icon => CieDiagramEditorTool.CreateIcon();

        public ICommand? Command { get; }

        private void OpenCieDiagram()
        {
            if (_windowCie == null)
            {
                _windowCie = new WindowCIE { Owner = Application.Current.GetActiveWindow() };

                _pixelSampleChangedHandler = (_, pixelSample) =>
                {
                    _ensureBufferLoaded();

                    CvcieMouseProbeOptions probeSettings = _getProbeSettings();
                    float dXVal = 0;
                    float dYVal = 0;
                    float dZVal = 0;
                    float dx = 0;
                    float dy = 0;
                    float du = 0;
                    float dv = 0;

                    _ = ConvertXYZ.CM_GetXYZxyuvRect(
                        _getConvertHandle(),
                        pixelSample.PixelX,
                        pixelSample.PixelY,
                        ref dXVal,
                        ref dYVal,
                        ref dZVal,
                        ref dx,
                        ref dy,
                        ref du,
                        ref dv,
                        probeSettings.RectWidth,
                        probeSettings.RectHeight);

                    _windowCie?.ChangeSelect(dx, dy);
                };

                _context.MouseInfoProvider.PixelSampleChanged += _pixelSampleChangedHandler;

                _windowCie.Closed += (_, _) =>
                {
                    if (_pixelSampleChangedHandler != null)
                    {
                        _context.MouseInfoProvider.PixelSampleChanged -= _pixelSampleChangedHandler;
                    }
                    _pixelSampleChangedHandler = null;
                    _windowCie = null;
                };
            }

            _windowCie.Show();
            _windowCie.Activate();
        }

        public void Deactivate()
        {
            _windowCie?.Close();
        }

        public void Dispose()
        {
            Deactivate();
            GC.SuppressFinalize(this);
        }
    }
}