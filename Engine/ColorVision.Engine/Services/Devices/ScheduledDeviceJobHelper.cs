using ColorVision.Engine.Messages;
using ColorVision.Scheduler;
using Quartz;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices
{
    internal static class ScheduledDeviceJobHelper
    {
        private const string SchedulerInfoKey = "SchedulerInfo";
        private static readonly object captureSync = new();
        private static readonly Dictionary<DeviceCaptureKey, CaptureReservation> capturesInProgress = new();

        public static SchedulerInfo GetSchedulerInfo(IJobExecutionContext context)
        {
            JobDataMap jobDataMap = context.JobDetail.JobDataMap;
            if (jobDataMap.TryGetValue(SchedulerInfoKey, out object? value) &&
                value is SchedulerInfo schedulerInfo)
            {
                return schedulerInfo;
            }

            throw new JobExecutionException("SchedulerInfo is missing from the job data map.");
        }

        public static Dispatcher GetApplicationDispatcher()
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                throw new JobExecutionException("The WPF application dispatcher is unavailable.");
            }

            return dispatcher;
        }

        internal static TDevice? ResolveDevice<TDevice>(IEnumerable<TDevice> devices, string? explicitCode,
            string? legacySelection, out bool ambiguous) where TDevice : DeviceService
        {
            ambiguous = false;
            bool hasExplicitCode = explicitCode != null;
            string? selection = explicitCode ?? legacySelection;
            if (string.IsNullOrWhiteSpace(selection)) return null;

            TDevice? match = null;
            foreach (TDevice device in devices)
            {
                bool matchesCode = string.Equals(device.Code, selection, StringComparison.Ordinal);
                bool matchesLegacyName = !hasExplicitCode && device.GetConfig() is DeviceServiceConfig config
                    && string.Equals(config.Name, selection, StringComparison.Ordinal);
                if (!matchesCode && !matchesLegacyName) continue;
                if (match != null && !ReferenceEquals(match, device))
                {
                    ambiguous = true;
                    return null;
                }
                match = device;
            }
            return match;
        }

        internal static JobExecutionException CreateDeviceSelectionException(bool ambiguous) => new(
            EngineLocalization.Get(ambiguous ? "Scheduler_DeviceReferenceAmbiguous" : "Scheduler_DeviceReferenceUnavailable"));

        internal static MsgRecord? DispatchCapture<TDevice>(TDevice device, Func<TDevice, MsgRecord?> capture) where TDevice : DeviceService
        {
            string code = device.Code;
            var key = string.IsNullOrWhiteSpace(code) ? new DeviceCaptureKey(typeof(TDevice), null, device)
                : new DeviceCaptureKey(typeof(TDevice), code, null);
            var reservation = new CaptureReservation(key);
            lock (captureSync)
            {
                if (capturesInProgress.TryGetValue(key, out var active) && active.Completion?.IsCompleted != true)
                    throw new JobExecutionException(EngineLocalization.Get("Scheduler_DeviceCaptureInProgress"));

                capturesInProgress[key] = reservation;
            }

            bool releaseOnCompletion = false;
            try
            {
                MsgRecord? record = capture(device);
                if (record == null) return null;

                Task<MsgRecordState> completion = GetTerminalCompletion(record);
                if (!completion.IsCompleted)
                {
                    // The command owns the reservation independently of the Quartz waiter's deadline.
                    lock (captureSync) reservation.Completion = completion;
                    _ = completion.ContinueWith(static (_, state) => ((CaptureReservation)state!).Release(), reservation,
                        CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                    releaseOnCompletion = true;
                }
                return record;
            }
            finally
            {
                if (!releaseOnCompletion) reservation.Release();
            }
        }

        private readonly record struct DeviceCaptureKey(Type DeviceType, string? Code, DeviceService? UncodedDevice);

        private sealed class CaptureReservation(DeviceCaptureKey key)
        {
            internal Task<MsgRecordState>? Completion { get; set; }

            internal void Release()
            {
                lock (captureSync)
                {
                    if (capturesInProgress.TryGetValue(key, out var active) && ReferenceEquals(active, this))
                        capturesInProgress.Remove(key);
                }
            }
        }

        public static TimeSpan GetTimeout(SchedulerInfo schedulerInfo)
        {
            return schedulerInfo.TimeoutSeconds > 0
                ? TimeSpan.FromSeconds(schedulerInfo.TimeoutSeconds)
                : Timeout.InfiniteTimeSpan;
        }

        public static async Task<MsgRecordState> WaitForTerminalStateAsync(
            MsgRecord msgRecord,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            try
            {
                return await GetTerminalCompletion(msgRecord).WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
            }
            catch (TimeoutException) when (msgRecord.WorkerCompletion == null)
            {
                // A legacy record may already know the outcome while the UI is still releasing the command lease.
                cancellationToken.ThrowIfCancellationRequested();
                MsgRecordState committedState = msgRecord.MsgRecordState;
                if (MsgRecord.IsTerminal(committedState)) return committedState;
                throw;
            }
        }

        internal static Task<MsgRecordState> GetTerminalCompletion(MsgRecord msgRecord) =>
            msgRecord.WorkerCompletion ?? msgRecord.LegacyNotificationCompletion;

        public static JobExecutionException CreateTerminalStateException(
            MsgRecord msgRecord,
            MsgRecordState state)
        {
            string? message = msgRecord.MsgReturn?.Message;
            if (string.IsNullOrWhiteSpace(message))
            {
                message = state == MsgRecordState.Timeout
                    ? Properties.Resources.Timeout
                    : Properties.Resources.Failure;
            }

            return new JobExecutionException(message);
        }
    }
}
