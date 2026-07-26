using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentToolBudgetCompletionGate
    {
        private readonly object _syncRoot = new();
        private readonly HashSet<int> _unfinishedRounds = new();
        private readonly Action? _onReadyToFinalize;
        private bool _isExhausted;
        private bool _finalizationSignaled;

        public CopilotAgentToolBudgetCompletionGate(Action? onReadyToFinalize)
        {
            _onReadyToFinalize = onReadyToFinalize;
        }

        public bool IsExhausted
        {
            get
            {
                lock (_syncRoot)
                    return _isExhausted;
            }
        }

        public void TrackReservedRound(int round)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(round);

            lock (_syncRoot)
            {
                if (!_unfinishedRounds.Add(round))
                    throw new InvalidOperationException($"Tool round {round} is already tracked.");
            }
        }

        public void CompleteRound(int round)
        {
            Action? callback;
            lock (_syncRoot)
            {
                _unfinishedRounds.Remove(round);
                callback = TakeFinalizationCallbackIfReady();
            }

            callback?.Invoke();
        }

        public void MarkExhausted()
        {
            Action? callback;
            lock (_syncRoot)
            {
                _isExhausted = true;
                callback = TakeFinalizationCallbackIfReady();
            }

            callback?.Invoke();
        }

        private Action? TakeFinalizationCallbackIfReady()
        {
            if (!_isExhausted || _unfinishedRounds.Count > 0 || _finalizationSignaled)
                return null;

            _finalizationSignaled = true;
            return _onReadyToFinalize;
        }
    }
}
