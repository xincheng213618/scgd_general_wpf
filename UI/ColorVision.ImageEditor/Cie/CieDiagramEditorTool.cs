using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieDiagramEditorTool : IEditorTool, IDisposable
    {
        private readonly EditorContext _context;
        private WindowCIE? _windowCie;
        private EventHandler<ImagePixelSample>? _pixelSampleChangedHandler;

        public CieDiagramEditorTool(EditorContext context)
        {
            _context = context;
            _context.Config.Cleared += Config_Cleared;
            Command = new RelayCommand(_ => OpenCieDiagram());
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;

        public string? GuidId => "CIE1931";

        public int Order => 0;

        public object? Icon => CreateIcon();

        public ICommand? Command { get; }

        private void OpenCieDiagram()
        {
            if (_windowCie == null)
            {
                _windowCie = new WindowCIE { Owner = Application.Current.GetActiveWindow() };

                _pixelSampleChangedHandler = (_, pixelSample) =>
                {
                    _windowCie?.ChangeSelect(pixelSample);
                };

                _context.MouseInfoProvider.PixelSampleChanged += _pixelSampleChangedHandler;

                _windowCie.Closed += WindowCie_Closed;
            }

            _windowCie.Show();
            _windowCie.Activate();
        }

        private void Config_Cleared(object? sender, EventArgs e)
        {
            Deactivate();
        }

        private void WindowCie_Closed(object? sender, EventArgs e)
        {
            if (_pixelSampleChangedHandler != null)
            {
                _context.MouseInfoProvider.PixelSampleChanged -= _pixelSampleChangedHandler;
            }

            if (_windowCie != null)
            {
                _windowCie.Closed -= WindowCie_Closed;
            }

            _pixelSampleChangedHandler = null;
            _windowCie = null;
        }

        public void Deactivate()
        {
            _windowCie?.Close();
        }

        public void Dispose()
        {
            _context.Config.Cleared -= Config_Cleared;
            Deactivate();
            GC.SuppressFinalize(this);
        }

        public static object CreateIcon()
        {
            return new Image
            {
                Source = new BitmapImage(new Uri("/ColorVision.ImageEditor;component/Assets/Image/CIE1931xy1.png", UriKind.Relative)),
                Style = Application.Current.TryFindResource("ToolBarImage") as Style
            };
        }
    }
}
