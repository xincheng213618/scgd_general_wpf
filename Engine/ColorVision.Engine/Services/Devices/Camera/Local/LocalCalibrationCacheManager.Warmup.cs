using ColorVision.Common.NativeMethods;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Camera.Local;

internal sealed partial class LocalCalibrationCacheManager
{
    private static readonly TimeSpan WarmupDelay = TimeSpan.FromSeconds(2);
    private const int WarmupAttempts = 5;
    private const int MinimumAvailableMemoryMiB = 2048;
    private const double MaximumBusyFraction = 0.5;
    private static int foregroundUsers;
    private static int backgroundUser;

    private readonly Lock warmupGate = new();
    private CancellationTokenSource? warmupRequest;

    internal void OnMeasurementOpened()
        => _ = ScheduleWarmup(_ => Task.FromResult(ReadExitPlans()), TimeProvider.System,
            IsSystemIdleAsync, static () => LocalCalibrationWarmupHistory.Enabled);

    internal void OnMeasurementClosed() => CancelWarmup();

    internal Task ScheduleWarmup(
        Func<CancellationToken, Task<IReadOnlyList<LocalCalibrationWarmupPlan>>> resolvePlans,
        TimeProvider timeProvider, Func<CancellationToken, Task<bool>> isIdle, Func<bool> isEnabled)
    {
        lock (warmupGate)
        {
            if (Volatile.Read(ref disposed) || warmupRequest != null) return Task.CompletedTask;
            var request = new CancellationTokenSource();
            warmupRequest = request;
            return WarmPlansAsync(request, resolvePlans, timeProvider, isIdle, isEnabled);
        }
    }

    private void CancelWarmup()
    {
        lock (warmupGate)
        {
            warmupRequest?.Cancel();
            warmupRequest = null;
        }
    }

    private async Task WarmPlansAsync(CancellationTokenSource request,
        Func<CancellationToken, Task<IReadOnlyList<LocalCalibrationWarmupPlan>>> resolvePlans,
        TimeProvider timeProvider, Func<CancellationToken, Task<bool>> isIdle, Func<bool> isEnabled)
    {
        CancellationToken token = request.Token;
        try
        {
            for (int attempt = 0; attempt < WarmupAttempts; attempt++)
            {
                await Task.Delay(WarmupDelay, timeProvider, token).ConfigureAwait(false);
                if (!isEnabled()) return;
                if (Volatile.Read(ref foregroundUsers) != 0 || !await isIdle(token).ConfigureAwait(false)) continue;
                if (Interlocked.CompareExchange(ref backgroundUser, 1, 0) != 0) continue;
                try
                {
                    var plans = await resolvePlans(token).ConfigureAwait(false);
                    bool first = true;
                    foreach (var plan in plans.Reverse())
                    {
                        token.ThrowIfCancellationRequested();
                        if (!isEnabled() || Volatile.Read(ref foregroundUsers) != 0
                            || LocalCameraCaptureService.IsCaptureInProgress) return;
                        if (!TryWarmPlan(plan, token, first)) return;
                        first = false;
                    }
                    return;
                }
                finally { Volatile.Write(ref backgroundUser, 0); }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { log.Debug($"Restoring the calibration cache for {deviceCode} failed.", ex); }
        finally
        {
            lock (warmupGate)
            {
                if (warmupRequest == request) warmupRequest = null;
                request.Dispose();
            }
        }
    }

    private bool TryWarmPlan(LocalCalibrationWarmupPlan plan, CancellationToken token, bool first)
    {
        if (!SharedCacheLifecycleGate.TryEnterReadLock(0)) return false;
        try
        {
            if (!executionGate.TryEnter()) return false;
            try
            {
                // Check again under the execution lock before starting I/O. The foreground can
                // await one already-running load, while every later speculative load is skipped.
                if (disposed || token.IsCancellationRequested || Volatile.Read(ref foregroundUsers) != 0
                    || (first && cache.CachedItemCount != 0)) return false;
                cache.Warm(plan, GetEntries());
            }
            catch (Exception ex)
            {
                log.Debug($"Preparing a cached calibration plan for {deviceCode} failed.", ex);
            }
            finally { executionGate.Exit(); }
            return true;
        }
        finally { SharedCacheLifecycleGate.ExitReadLock(); }
    }

    private IReadOnlyList<LocalCalibrationWarmupPlan> ReadExitPlans()
    {
        var plans = LocalCalibrationWarmupHistory.GetExitPlans(deviceCode);
        if (plans.Length == 0) return plans;
        ulong budget = ColorVision.Core.OpenCVCalibration.GetCalibrationSharedCacheStatistics().BudgetBytes;
        return LocalCalibrationWarmupHistory.FitPlans(plans, budget);
    }

    private static async Task<bool> IsSystemIdleAsync(CancellationToken token)
    {
        if (LocalCameraCaptureService.IsCaptureInProgress
            || PerformanceInfo.GetPhysicalAvailableMemoryInMiB() < MinimumAvailableMemoryMiB) return false;
        var before = FlowPerformanceSampler.ReadSystemCpu();
        await Task.Delay(TimeSpan.FromMilliseconds(250), token).ConfigureAwait(false);
        var after = FlowPerformanceSampler.ReadSystemCpu();
        if (before == null || after == null) return false;
        ulong total = after.Kernel + after.User - before.Kernel - before.User;
        ulong idle = after.Idle - before.Idle;
        return total > 0 && idle <= total && (double)(total - idle) / total <= MaximumBusyFraction;
    }

    private static void ValidateTemplate(IReadOnlyList<DeviceCameraCalibrationFile> files, string template)
    {
        if (files.Count == 0)
            throw new InvalidOperationException($"校正模板“{template}”没有选择任何校正文件。");
        if (files.Count(file => LocalFrameCalibrationService.IsColorCalibration(file.CalibrationType)) > 1)
            throw new InvalidOperationException($"校正模板“{template}”同时选择了多个亮度/颜色校正文件，本地校正只能使用一个。");
    }
}
