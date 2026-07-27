using ColorVision.Engine.Messages;
using ColorVision.Scheduler;
using Quartz;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Devices
{
    internal static class ScheduledDeviceJobHelper
    {
        private const string SchedulerInfoKey = "SchedulerInfo";

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
            var completion = new TaskCompletionSource<MsgRecordState>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            EventHandler<MsgRecordState>? stateChanged = null;
            stateChanged = (_, state) =>
            {
                if (IsTerminalState(state))
                {
                    completion.TrySetResult(state);
                }
            };

            msgRecord.MsgRecordStateChanged += stateChanged;
            try
            {
                // Close the race where the message reached a terminal state before subscription.
                stateChanged(msgRecord, msgRecord.MsgRecordState);
                return await completion.Task.WaitAsync(timeout, cancellationToken);
            }
            finally
            {
                msgRecord.MsgRecordStateChanged -= stateChanged;
            }
        }

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

        private static bool IsTerminalState(MsgRecordState state)
        {
            return state == MsgRecordState.Success ||
                   state == MsgRecordState.Fail ||
                   state == MsgRecordState.Timeout;
        }
    }
}
