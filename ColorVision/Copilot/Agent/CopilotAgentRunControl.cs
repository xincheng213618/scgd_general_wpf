using System;
using System.Threading;

namespace ColorVision.Copilot
{
    public enum CopilotAgentControlIntent
    {
        None,
        Pause,
        Cancel,
    }

    public sealed class CopilotAgentRunControl
    {
        private int _intent;

        public CopilotAgentControlIntent Intent => (CopilotAgentControlIntent)Volatile.Read(ref _intent);

        public bool RequestPause()
        {
            return Interlocked.CompareExchange(
                ref _intent,
                (int)CopilotAgentControlIntent.Pause,
                (int)CopilotAgentControlIntent.None) == (int)CopilotAgentControlIntent.None;
        }

        public bool RequestCancel()
        {
            var previous = (CopilotAgentControlIntent)Interlocked.Exchange(ref _intent, (int)CopilotAgentControlIntent.Cancel);
            return previous != CopilotAgentControlIntent.Cancel;
        }
    }
}
