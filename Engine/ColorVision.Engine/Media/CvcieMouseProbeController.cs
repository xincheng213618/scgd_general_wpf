using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using cvColorVision;
using System;

namespace ColorVision.Engine.Media
{
    internal sealed class CvcieMouseProbeController : IDisposable
    {
        private readonly ImageView _imageView;
        private readonly IntPtr _convertHandle;
        private readonly Action _ensureBufferLoaded;
        private readonly Func<float[]?> _getExp;
        private readonly Func<bool> _showDateFilePath;
        private readonly Func<int, int, (int pointIndex, int listIndex)> _findNearbyPoints;
        private readonly Func<CvcieProbeSettings> _getProbeSettings;

        public CvcieMouseProbeController(
            ImageView imageView,
            IntPtr convertHandle,
            Action ensureBufferLoaded,
            Func<float[]?> getExp,
            Func<bool> showDateFilePath,
            Func<int, int, (int pointIndex, int listIndex)> findNearbyPoints,
            Func<CvcieProbeSettings> getProbeSettings)
        {
            _imageView = imageView;
            _convertHandle = convertHandle;
            _ensureBufferLoaded = ensureBufferLoaded;
            _getExp = getExp;
            _showDateFilePath = showDateFilePath;
            _findNearbyPoints = findNearbyPoints;
            _getProbeSettings = getProbeSettings;
        }

        public bool TryHandleProbe(object sender, ImageInfo imageInfo)
        {
            var exp = _getExp();
            if (exp == null || exp.Length == 0)
            {
                return false;
            }

            var magnifier = _imageView.EditorContext.IEditorToolFactory.GetIEditorTool<MouseMagnifierManager>();
            if (magnifier == null)
            {
                return false;
            }

            var probeSettings = _getProbeSettings();

            _ensureBufferLoaded();

            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0;
            float dy = 0;
            float du = 0;
            float dv = 0;

            var (x2, y2) = _findNearbyPoints(imageInfo.X, imageInfo.Y);
            x2 += 1;
            y2 += 1;

            switch (probeSettings.MagnigifierType)
            {
                case MagnigifierType.Circle:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYCircle(_convertHandle, imageInfo.X, imageInfo.Y, ref dYVal, probeSettings.Radius);
                        magnifier.DrawImage(imageInfo, $"Y:{dYVal:F1}", string.Empty, probeSettings.MagnigifierType, probeSettings.Radius, probeSettings.RectWidth, probeSettings.RectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(_convertHandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, probeSettings.Radius);
                        string text1 = _showDateFilePath()
                            ? $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1},({x2},{y2})"
                            : $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
                        string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                        magnifier.DrawImage(imageInfo, text1, text2, probeSettings.MagnigifierType, probeSettings.Radius, probeSettings.RectWidth, probeSettings.RectHeight);
                    }
                    return true;
                case MagnigifierType.Rect:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYRect(_convertHandle, imageInfo.X, imageInfo.Y, ref dYVal, probeSettings.RectWidth, probeSettings.RectHeight);
                        magnifier.DrawImage(imageInfo, $"Y:{dYVal:F1}", string.Empty, probeSettings.MagnigifierType, probeSettings.Radius, probeSettings.RectWidth, probeSettings.RectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(_convertHandle, imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, probeSettings.RectWidth, probeSettings.RectHeight);
                        string text1 = _showDateFilePath()
                            ? $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1},({x2},{y2})"
                            : $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
                        string text2 = $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}";
                        magnifier.DrawImage(imageInfo, text1, text2, probeSettings.MagnigifierType, probeSettings.Radius, probeSettings.RectWidth, probeSettings.RectHeight);
                    }
                    return true;
                default:
                    return false;
            }
        }

        public void Dispose()
        {
        }
    }
}
