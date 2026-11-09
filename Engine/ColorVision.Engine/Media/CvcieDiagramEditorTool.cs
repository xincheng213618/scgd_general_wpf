using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Cie;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Media
{
    internal sealed class CvcieDiagramEditorTool : IEditorTool, IDisposable
    {
        private readonly EditorContext _context;
        private readonly CvcieProbeUpdates _updates;
        private readonly Func<PoiMeasurementPoint, (int Channels, PoiMeasurementResult Result)> _calculatePoi;
        private readonly Func<CvcieMouseProbeOptions> _getProbeSettings;
        private WindowCIE? _windowCie;
        private EventHandler<ImagePixelSample>? _pixelSampleChangedHandler;

        public CvcieDiagramEditorTool(
            EditorContext context,
            Func<ValueTask<bool>> prepare,
            Func<PoiMeasurementPoint, (int Channels, PoiMeasurementResult Result)> calculatePoi,
            Func<CvcieMouseProbeOptions> getProbeSettings)
        {
            _context = context;
            _updates = new CvcieProbeUpdates(context.ImageView, prepare, RenderSample);
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

                _pixelSampleChangedHandler = (_, pixelSample) => _updates.Update(pixelSample);

                _context.MouseInfoProvider.PixelSampleChanged += _pixelSampleChangedHandler;

                _windowCie.Closed += (_, _) =>
                {
                    _updates.Reset();
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

        private void RenderSample(ImagePixelSample pixelSample)
        {
            CvcieMouseProbeOptions probeSettings = _getProbeSettings();
            (int channels, PoiMeasurementResult measurement) = _calculatePoi(
                probeSettings.CreateMeasurementPoint(pixelSample.PixelX, pixelSample.PixelY));
            if (channels != 3)
            {
                _windowCie?.SetSelectedMarker(null);
                return;
            }

            _windowCie?.ChangeSelect(new CieXyz(measurement.X, measurement.Y, measurement.Z),
                $"POI ({pixelSample.PixelX}, {pixelSample.PixelY})", "CVCIE 原始 XYZ / POI");
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
