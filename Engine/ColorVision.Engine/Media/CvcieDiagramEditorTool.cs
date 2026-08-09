using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Media
{
    internal sealed class CvcieDiagramEditorTool : IEditorTool, IDisposable
    {
        private readonly EditorContext _context;
        private readonly Func<PoiMeasurementPoint, (int Channels, PoiMeasurementResult Result)> _calculatePoi;
        private readonly Func<CvcieMouseProbeOptions> _getProbeSettings;
        private WindowCIE? _windowCie;
        private EventHandler<ImagePixelSample>? _pixelSampleChangedHandler;

        public CvcieDiagramEditorTool(
            EditorContext context,
            Func<PoiMeasurementPoint, (int Channels, PoiMeasurementResult Result)> calculatePoi,
            Func<CvcieMouseProbeOptions> getProbeSettings)
        {
            _context = context;
            _calculatePoi = calculatePoi;
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
                    CvcieMouseProbeOptions probeSettings = _getProbeSettings();
                    PoiMeasurementResult measurement = _calculatePoi(
                        new PoiMeasurementPoint(
                            pixelSample.PixelX,
                            pixelSample.PixelY,
                            Math.Max(1, probeSettings.RectWidth),
                            Math.Max(1, probeSettings.RectHeight),
                            PoiMeasurementShape.Rect)).Result;

                    _windowCie?.ChangeSelect(measurement.ChromaX, measurement.ChromaY);
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
