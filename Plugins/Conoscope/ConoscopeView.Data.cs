#pragma warning disable CA1068,CS8604
using ColorVision.FileIO;
using Conoscope.Core;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Processing.Preprocess;
using OpenCvSharp;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Diagnostics;
using System.IO;
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
        private CancellationTokenSource? deferredXyzLoadCts;
        private int deferredXyzLoadVersion;

        public bool HasCaptureExposureSummary => !string.IsNullOrWhiteSpace(captureExposureSummary);
        public string CaptureExposureSummary => captureExposureSummary ?? Properties.Resources.StatusNotRecorded;

        private static CVCIEFile LoadCvcieFile(string filename)
        {
            CVFileUtil.Read(filename, out CVCIEFile fileInfo);

            if (fileInfo.Bpp != 32)
            {
                throw new InvalidDataException($"Conoscope only supports 32-bit float CVCIE data. Bpp={fileInfo.Bpp}.");
            }

            if (fileInfo.Channels < 3)
            {
                throw new InvalidDataException($"Conoscope CVCIE data requires at least 3 channels. Channels={fileInfo.Channels}.");
            }

            int channelSize = GetCvcieChannelByteCount(fileInfo);
            if (fileInfo.Data == null || fileInfo.Data.Length < channelSize * 3)
            {
                throw new InvalidDataException("CVCIE data length is insufficient for X/Y/Z channels.");
            }

            return fileInfo;
        }

        private static int GetCvcieChannelByteCount(CVCIEFile fileInfo)
        {
            long channelSize = checked((long)fileInfo.Rows * fileInfo.Cols * 4);
            if (channelSize > int.MaxValue)
            {
                throw new InvalidDataException("CVCIE channel data is too large.");
            }

            return (int)channelSize;
        }

        private static unsafe Mat CreateCvcieChannelMat(CVCIEFile fileInfo, int channelIndex)
        {
            int channelSize = GetCvcieChannelByteCount(fileInfo);
            fixed (byte* data = fileInfo.Data)
            {
                using Mat raw = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.CV_32FC1, (nint)(data + channelSize * channelIndex));
                return raw.Clone();
            }
        }

        private static string? FormatExposureSummary(CVCIEFile fileInfo)
        {
            if (fileInfo.Exp == null || fileInfo.Exp.Length == 0)
            {
                return null;
            }

            string[] values = new string[fileInfo.Exp.Length];
            for (int i = 0; i < fileInfo.Exp.Length; i++)
            {
                values[i] = fileInfo.Exp[i].ToString("F0");
            }

            return string.Join(",", values);
        }

        public void OpenConoscope(string filename, string? exposureSummary = null)
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch stageStopwatch = Stopwatch.StartNew();
            double loadMilliseconds = 0;
            double preprocessMilliseconds = 0;
            bool autoPreprocessApplied = false;
            bool deferredXyzStarted = false;

            try
            {
                Filename = filename;
                captureExposureSummary = string.IsNullOrWhiteSpace(exposureSummary) ? null : exposureSummary;
                PrepareDisplayStateForNewImage();
                HideCoordinateDragOverlay();
                DisposeCoordinateAxis();
                ImageView.Clear();
                LoadConoscopeDataForInitialDisplay(
                    filename,
                    out autoPreprocessApplied,
                    out deferredXyzStarted,
                    out loadMilliseconds,
                    out preprocessMilliseconds);
                applyCircleFitOnNextRefresh = true;

                stageStopwatch.Restart();
                EnsureSelectedDisplayChannelAvailable();

                RefreshDisplayedImage();
                UpdateContrastReferenceUi();
                SyncCieWindowFromCurrentPointer();
                StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);

                log.Info(
                    $"打开Conoscope图像完成: 文件={filename}, 尺寸={YMat?.Cols}x{YMat?.Rows}, 加载={loadMilliseconds:F0}ms, 预处理={preprocessMilliseconds:F0}ms, 渲染={stageStopwatch.Elapsed.TotalMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={autoPreprocessApplied}, 后台XZ={deferredXyzStarted}");
            }
            catch (Exception ex)
            {
                log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgOpenImageFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PrepareDisplayStateForNewImage()
        {
            RenderingConfig.DisplayChannel = ExportChannel.Y;
            currentReferenceScaleChannel = ExportChannel.Y;
            currentReferenceScaleMaximum = 1;

            if (!IsLoaded)
            {
                return;
            }

            RefreshDisplayControlsFromConfig();
            RaiseWindowQuickControlStateChanged();
        }

        private void LoadConoscopeDataForInitialDisplay(
            string filename,
            out bool autoPreprocessApplied,
            out bool deferredXyzStarted,
            out double loadMilliseconds,
            out double preprocessMilliseconds)
        {
            int loadVersion = BeginDeferredXyzLoad();
            ClearMatData(cancelDeferredLoad: false);

            Stopwatch stageStopwatch = Stopwatch.StartNew();
            CVCIEFile fileInfo = LoadCvcieFile(filename);
            captureExposureSummary ??= FormatExposureSummary(fileInfo);

            ConoscopePreprocessOptions options = CreatePreprocessOptions();
            Mat yMat = CreateCvcieChannelMat(fileInfo, 1);
            ClampNonPositiveChannelIfEnabled(yMat, options);
            loadMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;

            autoPreprocessApplied = PreprocessConfig.ApplyFilterOnOpen && HasPreprocessEnabled();
            preprocessMilliseconds = 0;
            if (autoPreprocessApplied)
            {
                stageStopwatch.Restart();
                OpenCvSharp.Mat? preprocessedY = yMat;
                ConoscopePreprocessPipeline.ApplyToSingleChannel(ref preprocessedY, options, log);
                yMat = preprocessedY!;
                preprocessMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;
            }

            YMat = yMat;

            log.Info($"已加载 CVCIE Y 数据: {fileInfo.Cols}x{fileInfo.Rows}, Bpp={fileInfo.Bpp}");

            StartDeferredXyzLoad(fileInfo, filename, options, autoPreprocessApplied, loadVersion);
            deferredXyzStarted = true;
        }

        private void LoadConoscopeData(string filename)
        {
            ClearMatData();
            using CVCIEFile fileInfo = LoadCvcieFile(filename);
            XMat = CreateCvcieChannelMat(fileInfo, 0);
            YMat = CreateCvcieChannelMat(fileInfo, 1);
            ZMat = CreateCvcieChannelMat(fileInfo, 2);
            captureExposureSummary ??= FormatExposureSummary(fileInfo);
            ClampNonPositiveXyzValuesIfEnabled();

            log.Info($"已加载 CVCIE XYZ 数据: {fileInfo.Cols}x{fileInfo.Rows}, Bpp={fileInfo.Bpp}");
        }

        private void RestoreOriginalMats()
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                return;
            }

            LoadConoscopeData(Filename);
        }

        private int BeginDeferredXyzLoad()
        {
            CancelDeferredXyzLoad();
            return deferredXyzLoadVersion;
        }

        private void StartDeferredXyzLoad(
            CVCIEFile fileInfo,
            string filename,
            ConoscopePreprocessOptions options,
            bool autoPreprocessApplied,
            int loadVersion)
        {
            CancellationTokenSource cts = new CancellationTokenSource();
            deferredXyzLoadCts = cts;
            _ = Task.Run(() => LoadDeferredXyzChannels(fileInfo, filename, options, autoPreprocessApplied, loadVersion, cts.Token), cts.Token);
        }

        private void LoadDeferredXyzChannels(
            CVCIEFile fileInfo,
            string filename,
            ConoscopePreprocessOptions options,
            bool autoPreprocessApplied,
            int loadVersion,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            OpenCvSharp.Mat? xMat = null;
            OpenCvSharp.Mat? yMat = null;
            OpenCvSharp.Mat? zMat = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                xMat = CreateCvcieChannelMat(fileInfo, 0);
                yMat = CreateCvcieChannelMat(fileInfo, 1);
                zMat = CreateCvcieChannelMat(fileInfo, 2);

                ClampNonPositiveChannelIfEnabled(xMat, options);
                ClampNonPositiveChannelIfEnabled(yMat, options);
                ClampNonPositiveChannelIfEnabled(zMat, options);

                if (autoPreprocessApplied)
                {
                    ConoscopePreprocessPipeline.Apply(ref xMat, ref yMat, ref zMat, options, log);
                }

                cancellationToken.ThrowIfCancellationRequested();

                OpenCvSharp.Mat completedXMat = xMat;
                OpenCvSharp.Mat completedZMat = zMat;
                xMat = null;
                zMat = null;

                Dispatcher.BeginInvoke(new Action(() => CompleteDeferredXyzLoad(
                    loadVersion,
                    cancellationToken,
                    filename,
                    autoPreprocessApplied,
                    stopwatch.Elapsed.TotalMilliseconds,
                    completedXMat,
                    completedZMat)));
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                log.Warn($"后台加载 Conoscope XZ 数据失败: 文件={filename}, 错误={ex.Message}", ex);
            }
            finally
            {
                yMat?.Dispose();
                xMat?.Dispose();
                zMat?.Dispose();
                fileInfo.Dispose();
                ReleaseDeferredXyzLoad(cancellationToken);
            }
        }

        private void CompleteDeferredXyzLoad(
            int loadVersion,
            CancellationToken cancellationToken,
            string filename,
            bool autoPreprocessApplied,
            double elapsedMilliseconds,
            OpenCvSharp.Mat xMat,
            OpenCvSharp.Mat zMat)
        {
            if (!IsCurrentDeferredXyzLoad(loadVersion, cancellationToken))
            {
                xMat.Dispose();
                zMat.Dispose();
                return;
            }

            XMat?.Dispose();
            ZMat?.Dispose();
            XMat = xMat;
            ZMat = zMat;

            RefreshChannelAvailability();
            UpdateContrastReferenceUi();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
            RaiseWindowQuickControlStateChanged();

            log.Info($"后台 XZ 初始化完成: 文件={filename}, 耗时={elapsedMilliseconds:F0}ms, 自动预处理={autoPreprocessApplied}");
        }

        private bool IsCurrentDeferredXyzLoad(int loadVersion, CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested && loadVersion == deferredXyzLoadVersion;
        }

        private void CancelDeferredXyzLoad()
        {
            deferredXyzLoadVersion++;

            CancellationTokenSource? cts = deferredXyzLoadCts;
            deferredXyzLoadCts = null;
            if (cts == null)
            {
                return;
            }

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cts.Dispose();
        }

        private void ReleaseDeferredXyzLoad(CancellationToken cancellationToken)
        {
            if (deferredXyzLoadCts == null || deferredXyzLoadCts.Token != cancellationToken)
            {
                return;
            }

            deferredXyzLoadCts.Dispose();
            deferredXyzLoadCts = null;
        }

        private static int ClampNonPositiveChannelIfEnabled(OpenCvSharp.Mat? channelMat, ConoscopePreprocessOptions options)
        {
            if (channelMat == null || !options.ClampNonPositiveXyz)
            {
                return 0;
            }

            return XyzClampProcessor.ClampNonPositive(channelMat, options.PositiveFloor);
        }

        private void ClearMatData(bool cancelDeferredLoad = true)
        {
            if (cancelDeferredLoad)
            {
                CancelDeferredXyzLoad();
            }

            XMat?.Dispose();
            XMat = null;
            YMat?.Dispose();
            YMat = null;
            ZMat?.Dispose();
            ZMat = null;
        }
    }
}
