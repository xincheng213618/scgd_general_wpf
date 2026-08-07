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
        private readonly object cvcieLoadSync = new();
        private readonly SemaphoreSlim cvcieLoadGate = new(1, 1);
        private CancellationTokenSource? deferredXyzLoadCts;
        private int deferredXyzLoadVersion;

        private sealed record CvcieLoadRequest(
            int Version,
            CancellationTokenSource Cancellation);

        private sealed record InitialCvcieLoadResult(
            Mat YMat,
            string? ExposureSummary,
            int Cols,
            int Rows,
            int Bpp,
            double LoadMilliseconds,
            double PreprocessMilliseconds,
            bool AutoPreprocessApplied,
            bool RequiresJointPreprocess);

        private sealed record DeferredCvcieLoadResult(
            Mat XMat,
            Mat? YMat,
            Mat ZMat,
            double ElapsedMilliseconds) : IDisposable
        {
            public void Dispose()
            {
                XMat.Dispose();
                YMat?.Dispose();
                ZMat.Dispose();
            }
        }

        public bool HasCaptureExposureSummary => !string.IsNullOrWhiteSpace(captureExposureSummary);
        public string CaptureExposureSummary => captureExposureSummary ?? Properties.Resources.StatusNotRecorded;

        private static CVCIEFile LoadCvcieChannelFile(
            string filename,
            int channelIndex,
            CancellationToken cancellationToken = default)
        {
            bool channelRead = CVFileUtil.ReadCIEFileChannel(filename, channelIndex, out CVCIEFile fileInfo, cancellationToken);
            try
            {
                if (fileInfo.Bpp != 32)
                {
                    throw new InvalidDataException($"Conoscope only supports 32-bit float CVCIE data. Bpp={fileInfo.Bpp}.");
                }

                if (fileInfo.Channels < 3)
                {
                    throw new InvalidDataException($"Conoscope CVCIE data requires at least 3 channels. Channels={fileInfo.Channels}.");
                }

                int channelSize = GetCvcieChannelByteCount(fileInfo);
                if (!channelRead || fileInfo.Data == null || fileInfo.Data.Length != channelSize)
                {
                    throw new InvalidDataException("CVCIE data length is insufficient for X/Y/Z channels.");
                }

                return fileInfo;
            }
            catch
            {
                fileInfo.Dispose();
                throw;
            }
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

        private static unsafe Mat CreateCvcieChannelMat(CVCIEFile fileInfo)
        {
            fixed (byte* data = fileInfo.Data)
            {
                using Mat raw = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.CV_32FC1, (nint)data);
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
            Filename = filename;
            captureExposureSummary = string.IsNullOrWhiteSpace(exposureSummary) ? null : exposureSummary;

            CvcieLoadRequest request = BeginDeferredXyzLoad();
            PrepareDisplayStateForNewImage();
            HideCoordinateDragOverlay();
            DisposeCoordinateAxis();
            ImageView.Clear();
            ClearMatData(cancelDeferredLoad: false);

            _ = OpenConoscopeAsync(filename, request);
        }

        private async Task OpenConoscopeAsync(string filename, CvcieLoadRequest request)
        {
            bool gateAcquired = false;
            bool initialDisplayCompleted = false;
            Stopwatch totalStopwatch = new Stopwatch();

            try
            {
                await cvcieLoadGate.WaitAsync(request.Cancellation.Token);
                gateAcquired = true;
                request.Cancellation.Token.ThrowIfCancellationRequested();
                totalStopwatch.Start();

                ConoscopePreprocessOptions options = CreatePreprocessOptions();
                bool autoPreprocessApplied = PreprocessConfig.ApplyFilterOnOpen && HasPreprocessEnabled();
                InitialCvcieLoadResult initial = await Task.Run(
                    () => LoadInitialCvcieChannel(filename, options, autoPreprocessApplied, request.Cancellation.Token),
                    request.Cancellation.Token);

                if (!IsCurrentDeferredXyzLoad(request))
                {
                    initial.YMat.Dispose();
                    return;
                }

                if (!initial.RequiresJointPreprocess)
                {
                    double renderMilliseconds = CommitInitialYDisplay(initial);
                    initialDisplayCompleted = true;
                    log.Info(
                        $"打开Conoscope图像完成: 文件={filename}, 尺寸={initial.Cols}x{initial.Rows}, 加载={initial.LoadMilliseconds:F0}ms, 预处理={initial.PreprocessMilliseconds:F0}ms, 渲染={renderMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={initial.AutoPreprocessApplied}, 后台XZ=True");
                }

                Mat? jointYMat = initial.RequiresJointPreprocess ? initial.YMat : null;
                DeferredCvcieLoadResult deferred = await Task.Run(
                    () => LoadDeferredXyzChannels(
                        filename,
                        options,
                        autoPreprocessApplied,
                        jointYMat,
                        request.Cancellation.Token));

                if (!IsCurrentDeferredXyzLoad(request))
                {
                    deferred.Dispose();
                    return;
                }

                if (deferred.YMat != null)
                {
                    double renderMilliseconds = CommitJointXyzDisplay(initial, deferred);
                    initialDisplayCompleted = true;
                    log.Info(
                        $"打开Conoscope图像完成: 文件={filename}, 尺寸={initial.Cols}x{initial.Rows}, 加载={initial.LoadMilliseconds:F0}ms, 预处理={initial.PreprocessMilliseconds + deferred.ElapsedMilliseconds:F0}ms, 渲染={renderMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={initial.AutoPreprocessApplied}, 后台XZ=False");
                }
                else
                {
                    CompleteDeferredXyzLoad(filename, autoPreprocessApplied, deferred);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (initialDisplayCompleted)
                {
                    log.Warn($"后台加载 Conoscope XZ 数据失败: 文件={filename}, 错误={ex.Message}", ex);
                }
                else
                {
                    log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                    MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgOpenImageFailed, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            finally
            {
                if (gateAcquired)
                {
                    cvcieLoadGate.Release();
                }

                ReleaseDeferredXyzLoad(request);
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

        private static InitialCvcieLoadResult LoadInitialCvcieChannel(
            string filename,
            ConoscopePreprocessOptions options,
            bool autoPreprocessApplied,
            CancellationToken cancellationToken)
        {
            Stopwatch stageStopwatch = Stopwatch.StartNew();
            Mat? yMat = null;
            try
            {
                using CVCIEFile fileInfo = LoadCvcieChannelFile(filename, 1, cancellationToken);
                yMat = CreateCvcieChannelMat(fileInfo);
                cancellationToken.ThrowIfCancellationRequested();
                ClampNonPositiveChannelIfEnabled(yMat, options);
                double loadMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;

                bool requiresJointPreprocess = autoPreprocessApplied && options.DustRemovalEnabled;
                double preprocessMilliseconds = 0;
                if (autoPreprocessApplied && !requiresJointPreprocess)
                {
                    stageStopwatch.Restart();
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref yMat, options, log);
                    preprocessMilliseconds = stageStopwatch.Elapsed.TotalMilliseconds;
                }

                cancellationToken.ThrowIfCancellationRequested();
                InitialCvcieLoadResult result = new InitialCvcieLoadResult(
                    yMat!,
                    FormatExposureSummary(fileInfo),
                    fileInfo.Cols,
                    fileInfo.Rows,
                    fileInfo.Bpp,
                    loadMilliseconds,
                    preprocessMilliseconds,
                    autoPreprocessApplied,
                    requiresJointPreprocess);
                yMat = null;
                return result;
            }
            finally
            {
                yMat?.Dispose();
            }
        }

        private double CommitInitialYDisplay(InitialCvcieLoadResult initial)
        {
            captureExposureSummary ??= initial.ExposureSummary;
            // Ownership moves from the background result to this view.
            YMat = initial.YMat;

            log.Info($"已加载 CVCIE Y 数据: {initial.Cols}x{initial.Rows}, Bpp={initial.Bpp}");

            return RefreshInitialDisplay();
        }

        private double CommitJointXyzDisplay(InitialCvcieLoadResult initial, DeferredCvcieLoadResult deferred)
        {
            captureExposureSummary ??= initial.ExposureSummary;
            // Ownership moves from the background result to this view.
            XMat = deferred.XMat;
            YMat = deferred.YMat;
            ZMat = deferred.ZMat;

            log.Info($"已加载 CVCIE XYZ 数据: {initial.Cols}x{initial.Rows}, Bpp={initial.Bpp}");

            return RefreshInitialDisplay();
        }

        private double RefreshInitialDisplay()
        {
            applyCircleFitOnNextRefresh = true;
            EnsureSelectedDisplayChannelAvailable();

            Stopwatch renderStopwatch = Stopwatch.StartNew();
            RefreshDisplayedImage();
            SyncCieWindowFromCurrentPointer();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
            return renderStopwatch.Elapsed.TotalMilliseconds;
        }

        private void LoadConoscopeData(string filename)
        {
            ClearMatData();
            Mat? xMat = null;
            Mat? yMat = null;
            Mat? zMat = null;
            try
            {
                using (CVCIEFile xFileInfo = LoadCvcieChannelFile(filename, 0))
                {
                    xMat = CreateCvcieChannelMat(xFileInfo);
                }

                int cols;
                int rows;
                int bpp;
                using (CVCIEFile yFileInfo = LoadCvcieChannelFile(filename, 1))
                {
                    yMat = CreateCvcieChannelMat(yFileInfo);
                    captureExposureSummary ??= FormatExposureSummary(yFileInfo);
                    cols = yFileInfo.Cols;
                    rows = yFileInfo.Rows;
                    bpp = yFileInfo.Bpp;
                }

                using (CVCIEFile zFileInfo = LoadCvcieChannelFile(filename, 2))
                {
                    zMat = CreateCvcieChannelMat(zFileInfo);
                }

                XMat = xMat;
                YMat = yMat;
                ZMat = zMat;
                xMat = null;
                yMat = null;
                zMat = null;
                ClampNonPositiveXyzValuesIfEnabled();

                log.Info($"已加载 CVCIE XYZ 数据: {cols}x{rows}, Bpp={bpp}");
            }
            finally
            {
                xMat?.Dispose();
                yMat?.Dispose();
                zMat?.Dispose();
            }
        }

        private void RestoreOriginalMats()
        {
            if (string.IsNullOrWhiteSpace(Filename))
            {
                return;
            }

            LoadConoscopeData(Filename);
        }

        private CvcieLoadRequest BeginDeferredXyzLoad()
        {
            lock (cvcieLoadSync)
            {
                deferredXyzLoadVersion++;
                deferredXyzLoadCts?.Cancel();

                CancellationTokenSource cancellation = new CancellationTokenSource();
                CvcieLoadRequest request = new CvcieLoadRequest(
                    deferredXyzLoadVersion,
                    cancellation);
                deferredXyzLoadCts = cancellation;
                return request;
            }
        }

        private static DeferredCvcieLoadResult LoadDeferredXyzChannels(
            string filename,
            ConoscopePreprocessOptions options,
            bool autoPreprocessApplied,
            Mat? jointYMat,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mat? xMat = null;
            Mat? yMat = jointYMat;
            Mat? zMat = null;

            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (CVCIEFile xFileInfo = LoadCvcieChannelFile(filename, 0, cancellationToken))
                {
                    xMat = CreateCvcieChannelMat(xFileInfo);
                }
                ClampNonPositiveChannelIfEnabled(xMat, options);
                cancellationToken.ThrowIfCancellationRequested();

                using (CVCIEFile zFileInfo = LoadCvcieChannelFile(filename, 2, cancellationToken))
                {
                    zMat = CreateCvcieChannelMat(zFileInfo);
                }
                ClampNonPositiveChannelIfEnabled(zMat, options);
                cancellationToken.ThrowIfCancellationRequested();

                if (autoPreprocessApplied && yMat != null)
                {
                    ConoscopePreprocessPipeline.Apply(ref xMat, ref yMat, ref zMat, options, log);
                }
                else if (autoPreprocessApplied)
                {
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref xMat, options, log);
                    cancellationToken.ThrowIfCancellationRequested();
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref zMat, options, log);
                }

                cancellationToken.ThrowIfCancellationRequested();
                DeferredCvcieLoadResult result = new DeferredCvcieLoadResult(
                    xMat!,
                    yMat,
                    zMat!,
                    stopwatch.Elapsed.TotalMilliseconds);
                xMat = null;
                yMat = null;
                zMat = null;
                return result;
            }
            finally
            {
                xMat?.Dispose();
                yMat?.Dispose();
                zMat?.Dispose();
            }
        }

        private void CompleteDeferredXyzLoad(
            string filename,
            bool autoPreprocessApplied,
            DeferredCvcieLoadResult deferred)
        {
            XMat?.Dispose();
            ZMat?.Dispose();
            // Ownership moves from the background result to this view.
            XMat = deferred.XMat;
            ZMat = deferred.ZMat;

            RefreshChannelAvailability();
            StatusBarItemsChanged?.Invoke(this, EventArgs.Empty);
            RaiseWindowQuickControlStateChanged();

            log.Info($"后台 XZ 初始化完成: 文件={filename}, 耗时={deferred.ElapsedMilliseconds:F0}ms, 自动预处理={autoPreprocessApplied}");
        }

        private bool IsCurrentDeferredXyzLoad(CvcieLoadRequest request)
        {
            lock (cvcieLoadSync)
            {
                return !request.Cancellation.IsCancellationRequested
                    && request.Version == deferredXyzLoadVersion
                    && ReferenceEquals(request.Cancellation, deferredXyzLoadCts);
            }
        }

        private void CancelDeferredXyzLoad()
        {
            lock (cvcieLoadSync)
            {
                deferredXyzLoadVersion++;
                deferredXyzLoadCts?.Cancel();
                deferredXyzLoadCts = null;
            }
        }

        private void ReleaseDeferredXyzLoad(CvcieLoadRequest request)
        {
            lock (cvcieLoadSync)
            {
                if (ReferenceEquals(deferredXyzLoadCts, request.Cancellation))
                {
                    deferredXyzLoadCts = null;
                }
            }

            request.Cancellation.Dispose();
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
