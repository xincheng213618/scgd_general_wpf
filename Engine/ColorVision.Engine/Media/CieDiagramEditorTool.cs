using ColorVision.Common.MVVM;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    public sealed class CieDiagramEditorTool : IEditorTool
    {
        private readonly EditorContext _context;
        private WindowCIE? _windowCie;

        public CieDiagramEditorTool(EditorContext context)
        {
            _context = context;
            Command = new RelayCommand(_ => OpenCieDiagram());
        }

        public ToolBarLocal ToolBarLocal => ToolBarLocal.Right;

        public string? GuidId => "CIE1931";

        public int Order => 0;

        public object? Icon => new Image
        {
            Source = new BitmapImage(new Uri("/ColorVision.ImageEditor;component/Assets/Image/CIE1931xy1.png", UriKind.Relative)),
            Style = Application.Current.TryFindResource("ToolBarImage") as Style
        };

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
                CvcieProbeSettings cvcieProbeSettings = CvcieProbeSettings.TryGet(_context.ImageView, out CvcieProbeSettings? settings)
                    ? settings
                    : CvcieProbeSettings.CreateFromDefaults();

                void MouseMoveColorHandler(object? sender, ImageInfo imageInfo)
                {
                    if (IsCvcieImage())
                    {
                        EnsureCvcieBufferLoaded();

                        float dXVal = 0;
                        float dYVal = 0;
                        float dZVal = 0;
                        float dx = 0;
                        float dy = 0;
                        float du = 0;
                        float dv = 0;

                        _ = ConvertXYZ.CM_GetXYZxyuvRect(
                            _context.Config.GetRequiredService<CVFilemageEditorConfig>().ConvertXYZhandle,
                            imageInfo.X,
                            imageInfo.Y,
                            ref dXVal,
                            ref dYVal,
                            ref dZVal,
                            ref dx,
                            ref dy,
                            ref du,
                            ref dv,
                            cvcieProbeSettings.RectWidth,
                            cvcieProbeSettings.RectHeight);

                        _windowCie?.ChangeSelect(dx, dy);
                    }
                    else
                    {
                        _windowCie?.ChangeSelect(imageInfo);
                    }
                }

                mouseMagnifier.MouseMoveColorHandler += MouseMoveColorHandler;

                _windowCie.Closed += (_, _) =>
                {
                    mouseMagnifier.MouseMoveColorHandler -= MouseMoveColorHandler;
                    mouseMagnifier.IsChecked = false;
                    _windowCie = null;
                };
            }

            _windowCie.Show();
            _windowCie.Activate();
        }

        private bool IsCvcieImage()
        {
            return _context.Config.GetProperties<bool>("IsCVCIE");
        }

        private void EnsureCvcieBufferLoaded()
        {
            if (_context.Config.GetProperties<bool>("IsBufferSet"))
            {
                return;
            }

            _context.Config.GetProperties<Action>("LoadBuffer")?.Invoke();
        }
    }
}
