using Conoscope.Core;
using Conoscope.Domain.Models;
using Conoscope.Infrastructure.FileIO;
using System;
using System.Diagnostics;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        public OpenCvSharp.Mat? XMat { get; set; }
        public OpenCvSharp.Mat? YMat { get; set; }
        public OpenCvSharp.Mat? ZMat { get; set; }

        string Filename = string.Empty;
        private string? captureExposureSummary;

        public bool HasCaptureExposureSummary => !string.IsNullOrWhiteSpace(captureExposureSummary);
        public string CaptureExposureSummary => captureExposureSummary ?? Properties.Resources.StatusNotRecorded;

        public void OpenConoscope(string filename, string? exposureSummary = null)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch stageStopwatch = Stopwatch.StartNew();
            double loadMilliseconds = 0;
            double preprocessMilliseconds = 0;
            bool autoPreprocessApplied = false;

            try
            {
                Filename = filename;
                captureExposureSummary = string.IsNullOrWhiteSpace(exposureSummary) ? null : exposureSummary;
                HideCoordinateDragOverlay();
                DisposeCoordinateAxis();
                ImageView.Clear();
                LoadConoscopeData(filename);
                loadMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;
                applyCircleFitOnNextRefresh = true;

                if (PreprocessConfig.ApplyFilterOnOpen && HasPreprocessEnabled())
                {
                    stageStopwatch.Restart();
                    ApplyPreprocessToCurrentMats();
                    preprocessMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;
                    autoPreprocessApplied = true;
                }

                stageStopwatch.Restart();
                EnsureSelectedDisplayChannelAvailable();

                RefreshDisplayedImage();
                UpdateContrastReferenceUi();
                SyncCieWindowFromCurrentPointer();
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);

                log.Info(
                    $"打开Conoscope图像完成: 文件={filename}, 尺寸={XMat?.Cols}x{XMat?.Rows}, 加载={loadMilliseconds:F0}ms, 预处理={preprocessMilliseconds:F0}ms, 渲染={stageStopwatch.Elapsed.TotalMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={autoPreprocessApplied}");
            }
            catch (Exception ex)
            {
                log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                MessageBox.Show(string.Format(Properties.Resources.MsgOpenImageFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadConoscopeData(string filename)
        {
            ClearMatData();

            using ConoscopeImageData data = CvcieImageLoader.Load(filename);
            (OpenCvSharp.Mat xMat, OpenCvSharp.Mat yMat, OpenCvSharp.Mat zMat) = data.Detach();
            XMat = xMat;
            YMat = yMat;
            ZMat = zMat;
            captureExposureSummary ??= data.ExposureSummary;
            ClampNonPositiveXyzValuesIfEnabled();

            log.Info($"已加载 CVCIE XYZ 数据: {data.Width}x{data.Height}, Bpp={data.BitsPerPixel}");
        }

        private void RestoreOriginalMats()
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                return;
            }

            LoadConoscopeData(Filename);
        }

        private void ClearMatData()
        {
            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
        }
    }
}
