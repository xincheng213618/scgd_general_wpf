using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw.Special;
using cvColorVision;
using System;

namespace ColorVision.Engine.Media
{
    internal sealed class CvcieMouseMagnifierManager : MouseMagnifierManager, IDisposable
    {
        private readonly Func<IntPtr> _getConvertHandle;
        private readonly Action _ensureBufferLoaded;
        private readonly Func<float[]?> _getExp;
        private readonly Func<bool> _showDateFilePath;
        private readonly Func<int, int, (int pointIndex, int listIndex)> _findNearbyPoints;
        private readonly Func<CvcieMouseProbeOptions> _getOptions;

        public CvcieMouseMagnifierManager(
            EditorContext editorContext,
            Func<IntPtr> getConvertHandle,
            Action ensureBufferLoaded,
            Func<float[]?> getExp,
            Func<bool> showDateFilePath,
            Func<int, int, (int pointIndex, int listIndex)> findNearbyPoints,
            Func<CvcieMouseProbeOptions> getOptions)
            : base(editorContext)
        {
            _getConvertHandle = getConvertHandle;
            _ensureBufferLoaded = ensureBufferLoaded;
            _getExp = getExp;
            _showDateFilePath = showDateFilePath;
            _findNearbyPoints = findNearbyPoints;
            _getOptions = getOptions;
        }

        public override string? GuidId => nameof(MouseMagnifierManager);

        protected override bool TryRenderOverlay(ImageInfo imageInfo)
        {
            float[]? exp = _getExp();
            if (exp == null || exp.Length == 0)
            {
                return false;
            }

            _ensureBufferLoaded();

            CvcieMouseProbeOptions options = _getOptions();
            double radius = Math.Max(1, options.Radius);
            int rectWidth = Math.Max(1, options.RectWidth);
            int rectHeight = Math.Max(1, options.RectHeight);

            float dXVal = 0;
            float dYVal = 0;
            float dZVal = 0;
            float dx = 0;
            float dy = 0;
            float du = 0;
            float dv = 0;

            switch (options.MagnigifierType)
            {
                case MagnigifierType.Circle:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYCircle(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dYVal, radius);
                        DrawProbeOverlay(imageInfo, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvCircle(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, radius);
                        DrawProbeOverlay(imageInfo, BuildPrimaryText(imageInfo, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                case MagnigifierType.Rect:
                    if (exp.Length == 1)
                    {
                        _ = ConvertXYZ.CM_GetYRect(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dYVal, rectWidth, rectHeight);
                        DrawProbeOverlay(imageInfo, $"Y:{dYVal:F1}", string.Empty, options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    else
                    {
                        _ = ConvertXYZ.CM_GetXYZxyuvRect(_getConvertHandle(), imageInfo.X, imageInfo.Y, ref dXVal, ref dYVal, ref dZVal, ref dx, ref dy, ref du, ref dv, rectWidth, rectHeight);
                        DrawProbeOverlay(imageInfo, BuildPrimaryText(imageInfo, dXVal, dYVal, dZVal), $"x:{dx:F2},y:{dy:F2},u:{du:F2},v:{dv:F2}", options.MagnigifierType, radius, rectWidth, rectHeight);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private string BuildPrimaryText(ImageInfo imageInfo, float dXVal, float dYVal, float dZVal)
        {
            string text = $"X:{dXVal:F1},Y:{dYVal:F1},Z:{dZVal:F1}";
            if (!_showDateFilePath())
            {
                return text;
            }

            (int pointIndex, int listIndex) = _findNearbyPoints(imageInfo.X, imageInfo.Y);
            if (pointIndex < 0 || listIndex < 0)
            {
                return text;
            }

            return $"{text},({pointIndex + 1},{listIndex + 1})";
        }

        public void Dispose()
        {
            IsChecked = false;
            GC.SuppressFinalize(this);
        }
    }
}