using ColorVision.FileIO;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Processing.Preprocess;
using log4net;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Conoscope
{
    internal enum ConoscopeDocumentChangeKind
    {
        InitialDisplayReady,
        DeferredChannelsReady
    }

    internal sealed class ConoscopeDocumentChangedEventArgs : EventArgs
    {
        public ConoscopeDocumentChangedEventArgs(ConoscopeDocumentChangeKind kind)
        {
            Kind = kind;
        }

        public ConoscopeDocumentChangeKind Kind { get; }
    }

    internal sealed class ConoscopeDocumentLoadFailedEventArgs : EventArgs
    {
        public ConoscopeDocumentLoadFailedEventArgs(Exception exception, bool initialDisplayCompleted)
        {
            Exception = exception;
            InitialDisplayCompleted = initialDisplayCompleted;
        }

        public Exception Exception { get; }
        public bool InitialDisplayCompleted { get; }
    }

    /// <summary>
    /// Owns one CVCIE document and its Mat lifetime. Loading remains latest-wins and
    /// publishes Y before X/Z whenever preprocessing allows it.
    /// </summary>
    internal sealed class ConoscopeDocument : IDisposable
    {
        private readonly ILog log;
        private readonly object loadSync = new();
        private readonly SemaphoreSlim loadGate = new(1, 1);
        private CancellationTokenSource? loadCancellation;
        private int loadVersion;
        private int dataVersion;

        public ConoscopeDocument(ILog log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public Mat? X { get; private set; }
        public Mat? Y { get; private set; }
        public Mat? Z { get; private set; }
        public string FileName { get; private set; } = string.Empty;
        public string? ExposureSummary { get; private set; }
        public bool HasDisplayData => Y != null;
        public bool HasXyzData => X != null && Y != null && Z != null;
        public int DataVersion => Volatile.Read(ref dataVersion);

        public event EventHandler<ConoscopeDocumentChangedEventArgs>? Changed;
        public event EventHandler<ConoscopeDocumentLoadFailedEventArgs>? LoadFailed;

        public Task OpenAsync(
            string fileName,
            string? exposureSummary,
            ConoscopePreprocessOptions options,
            bool applyPreprocess)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

            string? requestedExposureSummary = string.IsNullOrWhiteSpace(exposureSummary) ? null : exposureSummary;
            LoadRequest request = BeginLoad();
            ClearData(cancelPendingLoad: false);
            FileName = string.Empty;
            ExposureSummary = null;
            return LoadAsync(fileName, requestedExposureSummary, options, applyPreprocess, request);
        }

        public void Reload(ConoscopePreprocessOptions options)
        {
            string fileName = FileName;
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return;
            }

            CancelPendingLoad();
            ClearData(cancelPendingLoad: false);

            Mat? x = null;
            Mat? y = null;
            Mat? z = null;
            try
            {
                using (CVCIEFile file = ReadChannel(fileName, 0))
                {
                    x = CreateMat(file);
                }

                int cols;
                int rows;
                int bpp;
                using (CVCIEFile file = ReadChannel(fileName, 1))
                {
                    y = CreateMat(file);
                    ExposureSummary ??= FormatExposureSummary(file);
                    cols = file.Cols;
                    rows = file.Rows;
                    bpp = file.Bpp;
                }

                using (CVCIEFile file = ReadChannel(fileName, 2))
                {
                    z = CreateMat(file);
                }

                int clampedX = ClampIfEnabled(x, options);
                int clampedY = ClampIfEnabled(y, options);
                int clampedZ = ClampIfEnabled(z, options);
                if (clampedX + clampedY + clampedZ > 0)
                {
                    log.Warn($"加载时已将 XYZ<=0 修正为 {options.PositiveFloor}: X={clampedX}, Y={clampedY}, Z={clampedZ}");
                }

                X = x;
                Y = y;
                Z = z;
                x = null;
                y = null;
                z = null;
                MarkDataChanged();
                log.Info($"已加载 CVCIE XYZ 数据: {cols}x{rows}, Bpp={bpp}");
            }
            finally
            {
                x?.Dispose();
                y?.Dispose();
                z?.Dispose();
            }
        }

        public void ApplyPreprocess(ConoscopePreprocessOptions options)
        {
            Mat? x = X;
            Mat? y = Y;
            Mat? z = Z;
            try
            {
                ConoscopePreprocessPipeline.Apply(ref x, ref y, ref z, options, log);
            }
            finally
            {
                // The pipeline publishes each successfully replaced channel through
                // its ref argument. Keep the document on valid Mats even if a later
                // channel fails instead of retaining a disposed reference.
                X = x;
                Y = y;
                Z = z;
                MarkDataChanged();
            }
        }

        public void Dispose()
        {
            CancelPendingLoad();
            ClearData(cancelPendingLoad: false);
        }

        private async Task LoadAsync(
            string fileName,
            string? requestedExposureSummary,
            ConoscopePreprocessOptions options,
            bool applyPreprocess,
            LoadRequest request)
        {
            bool gateAcquired = false;
            bool initialDisplayCompleted = false;
            Stopwatch totalStopwatch = new();

            try
            {
                await loadGate.WaitAsync(request.Cancellation.Token);
                gateAcquired = true;
                request.Cancellation.Token.ThrowIfCancellationRequested();
                totalStopwatch.Start();

                InitialLoadResult initial = await Task.Run(
                    () => LoadInitialChannel(fileName, options, applyPreprocess, request.Cancellation.Token),
                    request.Cancellation.Token);

                if (!initial.RequiresJointPreprocess)
                {
                    if (!TryCommitInitial(request, fileName, requestedExposureSummary ?? initial.ExposureSummary, initial.Y))
                    {
                        initial.Y.Dispose();
                        return;
                    }

                    initialDisplayCompleted = true;
                    PublishChanged(ConoscopeDocumentChangeKind.InitialDisplayReady);
                    log.Info(
                        $"打开Conoscope图像完成: 文件={fileName}, 尺寸={initial.Cols}x{initial.Rows}, 加载={initial.LoadMilliseconds:F0}ms, 预处理={initial.PreprocessMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={initial.AutoPreprocessApplied}, 后台XZ=True");
                }
                else if (!IsCurrent(request))
                {
                    initial.Y.Dispose();
                    return;
                }

                Mat? jointY = initial.RequiresJointPreprocess ? initial.Y : null;
                // Do not pass the token to Task.Run here: jointY ownership has moved
                // into the delegate and its finally block must run even if cancellation
                // happens before the work is scheduled.
                DeferredLoadResult deferred = await Task.Run(
                    () => LoadDeferredChannels(fileName, options, applyPreprocess, jointY, request.Cancellation.Token));

                if (deferred.Y != null)
                {
                    if (!TryCommitFull(request, fileName, requestedExposureSummary ?? initial.ExposureSummary, deferred))
                    {
                        deferred.Dispose();
                        return;
                    }

                    initialDisplayCompleted = true;
                    PublishChanged(ConoscopeDocumentChangeKind.InitialDisplayReady);
                    log.Info(
                        $"打开Conoscope图像完成: 文件={fileName}, 尺寸={initial.Cols}x{initial.Rows}, 加载={initial.LoadMilliseconds:F0}ms, 预处理={initial.PreprocessMilliseconds + deferred.ElapsedMilliseconds:F0}ms, 总耗时={totalStopwatch.Elapsed.TotalMilliseconds:F0}ms, 自动预处理={initial.AutoPreprocessApplied}, 后台XZ=False");
                }
                else
                {
                    if (!TryCommitDeferred(request, deferred))
                    {
                        deferred.Dispose();
                        return;
                    }

                    PublishChanged(ConoscopeDocumentChangeKind.DeferredChannelsReady);
                    log.Info($"后台 XZ 初始化完成: 文件={fileName}, 耗时={deferred.ElapsedMilliseconds:F0}ms, 自动预处理={applyPreprocess}");
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!IsCurrent(request))
                {
                    return;
                }

                if (initialDisplayCompleted)
                {
                    log.Warn($"后台加载 Conoscope XZ 数据失败: 文件={fileName}, 错误={ex.Message}", ex);
                }
                else
                {
                    log.Error($"打开Conoscope图像失败: {ex.Message}", ex);
                }

                PublishLoadFailed(ex, initialDisplayCompleted);
            }
            finally
            {
                if (gateAcquired)
                {
                    loadGate.Release();
                }

                Release(request);
            }
        }

        private static InitialLoadResult LoadInitialChannel(
            string fileName,
            ConoscopePreprocessOptions options,
            bool applyPreprocess,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mat? y = null;
            try
            {
                using CVCIEFile file = ReadChannel(fileName, 1, cancellationToken);
                y = CreateMat(file);
                cancellationToken.ThrowIfCancellationRequested();
                ClampIfEnabled(y, options);
                double loadMilliseconds = stopwatch.Elapsed.TotalMilliseconds;

                bool requiresJointPreprocess = applyPreprocess && options.DustRemovalEnabled;
                double preprocessMilliseconds = 0;
                if (applyPreprocess && !requiresJointPreprocess)
                {
                    stopwatch.Restart();
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref y, options);
                    preprocessMilliseconds = stopwatch.Elapsed.TotalMilliseconds;
                }

                cancellationToken.ThrowIfCancellationRequested();
                InitialLoadResult result = new(
                    y!,
                    FormatExposureSummary(file),
                    file.Cols,
                    file.Rows,
                    loadMilliseconds,
                    preprocessMilliseconds,
                    applyPreprocess,
                    requiresJointPreprocess);
                y = null;
                return result;
            }
            finally
            {
                y?.Dispose();
            }
        }

        private static DeferredLoadResult LoadDeferredChannels(
            string fileName,
            ConoscopePreprocessOptions options,
            bool applyPreprocess,
            Mat? jointY,
            CancellationToken cancellationToken)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            Mat? x = null;
            Mat? y = jointY;
            Mat? z = null;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (CVCIEFile file = ReadChannel(fileName, 0, cancellationToken))
                {
                    x = CreateMat(file);
                }
                ClampIfEnabled(x, options);
                cancellationToken.ThrowIfCancellationRequested();

                using (CVCIEFile file = ReadChannel(fileName, 2, cancellationToken))
                {
                    z = CreateMat(file);
                }
                ClampIfEnabled(z, options);
                cancellationToken.ThrowIfCancellationRequested();

                if (applyPreprocess && y != null)
                {
                    ConoscopePreprocessPipeline.Apply(ref x, ref y, ref z, options);
                }
                else if (applyPreprocess)
                {
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref x, options);
                    cancellationToken.ThrowIfCancellationRequested();
                    ConoscopePreprocessPipeline.ApplyToSingleChannel(ref z, options);
                }

                cancellationToken.ThrowIfCancellationRequested();
                DeferredLoadResult result = new(x!, y, z!, stopwatch.Elapsed.TotalMilliseconds);
                x = null;
                y = null;
                z = null;
                return result;
            }
            finally
            {
                x?.Dispose();
                y?.Dispose();
                z?.Dispose();
            }
        }

        private static CVCIEFile ReadChannel(
            string fileName,
            int channelIndex,
            CancellationToken cancellationToken = default)
        {
            bool channelRead = CVFileUtil.ReadCIEFileChannel(fileName, channelIndex, out CVCIEFile file, cancellationToken);
            try
            {
                if (file.Bpp != 32)
                {
                    throw new InvalidDataException($"Conoscope only supports 32-bit float CVCIE data. Bpp={file.Bpp}.");
                }

                if (file.Channels < 3)
                {
                    throw new InvalidDataException($"Conoscope CVCIE data requires at least 3 channels. Channels={file.Channels}.");
                }

                long channelSize = checked((long)file.Rows * file.Cols * sizeof(float));
                if (channelSize > int.MaxValue)
                {
                    throw new InvalidDataException("CVCIE channel data is too large.");
                }

                if (!channelRead || file.Data == null || file.Data.Length != (int)channelSize)
                {
                    throw new InvalidDataException("CVCIE data length is insufficient for X/Y/Z channels.");
                }

                return file;
            }
            catch
            {
                file.Dispose();
                throw;
            }
        }

        private static unsafe Mat CreateMat(CVCIEFile file)
        {
            fixed (byte* data = file.Data)
            {
                using Mat raw = Mat.FromPixelData(file.Rows, file.Cols, MatType.CV_32FC1, (nint)data);
                return raw.Clone();
            }
        }

        private static int ClampIfEnabled(Mat? channel, ConoscopePreprocessOptions options)
        {
            return channel == null || !options.ClampNonPositiveXyz
                ? 0
                : XyzClampProcessor.ClampNonPositive(channel, options.PositiveFloor);
        }

        private static string? FormatExposureSummary(CVCIEFile file)
        {
            if (file.Exp == null || file.Exp.Length == 0)
            {
                return null;
            }

            string[] values = new string[file.Exp.Length];
            for (int index = 0; index < file.Exp.Length; index++)
            {
                values[index] = file.Exp[index].ToString("F0");
            }

            return string.Join(",", values);
        }

        private LoadRequest BeginLoad()
        {
            lock (loadSync)
            {
                loadVersion++;
                loadCancellation?.Cancel();
                CancellationTokenSource cancellation = new();
                loadCancellation = cancellation;
                return new LoadRequest(loadVersion, cancellation);
            }
        }

        private bool IsCurrent(LoadRequest request)
        {
            lock (loadSync)
            {
                return IsCurrentCore(request);
            }
        }

        private bool TryCommitInitial(LoadRequest request, string fileName, string? exposureSummary, Mat y)
        {
            lock (loadSync)
            {
                if (!IsCurrentCore(request))
                {
                    return false;
                }

                FileName = fileName;
                ExposureSummary = exposureSummary;
                Y = y;
                MarkDataChanged();
                return true;
            }
        }

        private bool TryCommitFull(LoadRequest request, string fileName, string? exposureSummary, DeferredLoadResult data)
        {
            lock (loadSync)
            {
                if (!IsCurrentCore(request))
                {
                    return false;
                }

                X?.Dispose();
                Y?.Dispose();
                Z?.Dispose();
                FileName = fileName;
                ExposureSummary = exposureSummary;
                X = data.X;
                Y = data.Y;
                Z = data.Z;
                MarkDataChanged();
                return true;
            }
        }

        private bool TryCommitDeferred(LoadRequest request, DeferredLoadResult data)
        {
            lock (loadSync)
            {
                if (!IsCurrentCore(request))
                {
                    return false;
                }

                X?.Dispose();
                Z?.Dispose();
                X = data.X;
                Z = data.Z;
                MarkDataChanged();
                return true;
            }
        }

        private bool IsCurrentCore(LoadRequest request)
        {
            return !request.Cancellation.IsCancellationRequested
                && request.Version == loadVersion
                && ReferenceEquals(request.Cancellation, loadCancellation);
        }

        private void PublishChanged(ConoscopeDocumentChangeKind kind)
        {
            EventHandler<ConoscopeDocumentChangedEventArgs>? handlers = Changed;
            if (handlers == null)
            {
                return;
            }

            ConoscopeDocumentChangedEventArgs args = new(kind);
            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((EventHandler<ConoscopeDocumentChangedEventArgs>)subscriber)(this, args);
                }
                catch (Exception ex)
                {
                    log.Error($"Conoscope 文档变更观察者执行失败: kind={kind}, error={ex.Message}", ex);
                }
            }
        }

        private void PublishLoadFailed(Exception exception, bool initialDisplayCompleted)
        {
            EventHandler<ConoscopeDocumentLoadFailedEventArgs>? handlers = LoadFailed;
            if (handlers == null)
            {
                return;
            }

            ConoscopeDocumentLoadFailedEventArgs args = new(exception, initialDisplayCompleted);
            foreach (Delegate subscriber in handlers.GetInvocationList())
            {
                try
                {
                    ((EventHandler<ConoscopeDocumentLoadFailedEventArgs>)subscriber)(this, args);
                }
                catch (Exception ex)
                {
                    log.Error($"Conoscope 文档加载失败观察者执行失败: {ex.Message}", ex);
                }
            }
        }

        private void MarkDataChanged()
        {
            Interlocked.Increment(ref dataVersion);
        }

        private void CancelPendingLoad()
        {
            lock (loadSync)
            {
                loadVersion++;
                loadCancellation?.Cancel();
                loadCancellation = null;
            }
        }

        private void Release(LoadRequest request)
        {
            lock (loadSync)
            {
                if (ReferenceEquals(loadCancellation, request.Cancellation))
                {
                    loadCancellation = null;
                }
            }

            request.Cancellation.Dispose();
        }

        private void ClearData(bool cancelPendingLoad = true)
        {
            if (cancelPendingLoad)
            {
                CancelPendingLoad();
            }

            X?.Dispose();
            X = null;
            Y?.Dispose();
            Y = null;
            Z?.Dispose();
            Z = null;
            MarkDataChanged();
        }

        private sealed record LoadRequest(int Version, CancellationTokenSource Cancellation);

        private sealed record InitialLoadResult(
            Mat Y,
            string? ExposureSummary,
            int Cols,
            int Rows,
            double LoadMilliseconds,
            double PreprocessMilliseconds,
            bool AutoPreprocessApplied,
            bool RequiresJointPreprocess);

        private sealed record DeferredLoadResult(Mat X, Mat? Y, Mat Z, double ElapsedMilliseconds) : IDisposable
        {
            public void Dispose()
            {
                X.Dispose();
                Y?.Dispose();
                Z.Dispose();
            }
        }
    }
}
