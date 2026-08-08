using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotAutomaticApprovalDenialCircuitBreakerSnapshot(
        bool IsTripped,
        int ConsecutiveDenials,
        int DenialsInWindow,
        int ReviewsInWindow)
    {
        public string FormatDiagnostic() =>
            $"Auto-review denial circuit breaker interrupted the Agent turn after {ConsecutiveDenials} consecutive denial(s)"
            + $" and {DenialsInWindow} denial(s) across the last {ReviewsInWindow} review(s);"
            + " no denied action was executed or retried.";

        public string FormatUserMessage() =>
            $"自动审查拒绝已达到本轮安全上限（连续 {ConsecutiveDenials} 次；最近 {ReviewsInWindow} 次审查中 {DenialsInWindow} 次拒绝）。"
            + "当前操作未执行，本轮已中断；请改用实质上更安全的方案，或停止并请用户确认后再继续。";
    }

    internal sealed class CopilotAutomaticApprovalDenialCircuitBreaker
    {
        internal const int ConsecutiveDenialLimit = 3;
        internal const int DenialLimitInWindow = 10;
        internal const int ReviewWindowSize = 50;

        private readonly Queue<bool> _recentDenials = new();
        private int _consecutiveDenials;
        private int _denialsInWindow;
        private bool _isTripped;

        public CopilotAutomaticApprovalDenialCircuitBreakerSnapshot Observe(
            CopilotAutomaticApprovalReviewVerdict verdict)
        {
            if (_isTripped)
                return CreateSnapshot();

            var denied = verdict == CopilotAutomaticApprovalReviewVerdict.Deny;
            _consecutiveDenials = denied ? _consecutiveDenials + 1 : 0;
            _recentDenials.Enqueue(denied);
            if (denied)
                _denialsInWindow++;
            if (_recentDenials.Count > ReviewWindowSize
                && _recentDenials.Dequeue())
            {
                _denialsInWindow--;
            }

            _isTripped = _consecutiveDenials >= ConsecutiveDenialLimit
                || _denialsInWindow >= DenialLimitInWindow;
            return CreateSnapshot();
        }

        private CopilotAutomaticApprovalDenialCircuitBreakerSnapshot CreateSnapshot() =>
            new(
                _isTripped,
                _consecutiveDenials,
                _denialsInWindow,
                _recentDenials.Count);
    }
}
