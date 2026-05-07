using ColorVision.Common.MVVM;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public sealed class CieDiagramEditorTool : IEditorTool, IDisposable
    {
        private readonly EditorContext _context;
        private WindowCIE? _windowCie;
        private MouseMagnifierManager? _mouseMagnifier;
        private MouseMoveColorHandler? _mouseMoveColorHandler;

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
            MouseMagnifierManager? mouseMagnifier = _context.IEditorToolFactory.GetIEditorTool<MouseMagnifierManager>();
            if (mouseMagnifier == null)
            {
                return;
            }

            if (_windowCie == null)
            {
                _windowCie = new WindowCIE { Owner = Application.Current.GetActiveWindow() };
                _mouseMagnifier = mouseMagnifier;

                _mouseMoveColorHandler = (_, imageInfo) =>
                {
                    _windowCie?.ChangeSelect(imageInfo);
                };

                _mouseMagnifier.MouseMoveColorHandler += _mouseMoveColorHandler;

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
            if (_mouseMagnifier != null && _mouseMoveColorHandler != null)
            {
                _mouseMagnifier.MouseMoveColorHandler -= _mouseMoveColorHandler;
                _mouseMagnifier.IsChecked = false;
            }

            if (_windowCie != null)
            {
                _windowCie.Closed -= WindowCie_Closed;
            }

            _mouseMoveColorHandler = null;
            _mouseMagnifier = null;
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