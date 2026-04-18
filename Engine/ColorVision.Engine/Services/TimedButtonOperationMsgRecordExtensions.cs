using ColorVision.Engine.Messages;
using ColorVision.UI;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    internal static class TimedButtonOperationMsgRecordExtensions
    {
        public static TimedButtonOperationScope? SendTimedCommand(
            this FrameworkElement owner,
            Button button,
            MsgRecord msgRecord,
            string? runningText = null,
            Action<MsgRecordState>? onTerminalStateChanged = null)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(button);
            ArgumentNullException.ThrowIfNull(msgRecord);

            TimedButtonOperationScope? operationScope = owner.GetTimedButtonOperations().Begin(button, runningText: runningText);

            msgRecord.MsgRecordStateChanged += (_, state) =>
            {
                if (!state.IsTerminalState())
                {
                    return;
                }

                operationScope?.Complete(state == MsgRecordState.Success);
                onTerminalStateChanged?.Invoke(state);
            };

            ServicesHelper.SendCommand(button, msgRecord);
            return operationScope;
        }

        public static bool IsTerminalState(this MsgRecordState state)
        {
            return state == MsgRecordState.Success || state == MsgRecordState.Fail || state == MsgRecordState.Timeout;
        }
    }
}