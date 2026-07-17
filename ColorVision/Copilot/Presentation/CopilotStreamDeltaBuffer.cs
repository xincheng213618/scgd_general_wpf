using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotStreamDeltaBuffer
    {
        public const int DefaultMaximumPendingCharacters = 64 * 1024;
        public const int DefaultMaximumPendingSegments = 64;

        private readonly CopilotUiBatchBuffer<CopilotStreamDelta> _buffer;

        public CopilotStreamDeltaBuffer(
            SynchronizationContext? targetContext,
            Action<IReadOnlyList<CopilotStreamDelta>> applyBatch,
            int maximumPendingCharacters = DefaultMaximumPendingCharacters,
            int maximumPendingSegments = DefaultMaximumPendingSegments,
            Func<bool>? isOnTargetThread = null)
        {
            _buffer = new CopilotUiBatchBuffer<CopilotStreamDelta>(
                targetContext,
                applyBatch,
                new PendingDeltaBatch(),
                maximumPendingCharacters,
                maximumPendingSegments,
                postOnTargetThread: false,
                isOnTargetThread);
        }

        public void Enqueue(CopilotStreamDelta delta)
        {
            if (delta.HasAny)
                _buffer.Enqueue(delta);
        }

        public Task CompleteAsync() => _buffer.CompleteAsync();

        private sealed class PendingDeltaBatch : ICopilotPendingBatch<CopilotStreamDelta>
        {
            private readonly List<PendingSegment> _segments = new();

            public int Count => _segments.Count;

            public int Size { get; private set; }

            public void Add(CopilotStreamDelta delta)
            {
                Size += (delta.ReasoningContent?.Length ?? 0) + (delta.Content?.Length ?? 0);
                if (_segments.Count > 0 && _segments[^1].CanAppend(delta))
                {
                    _segments[^1].Append(delta);
                    return;
                }

                _segments.Add(new PendingSegment(delta));
            }

            public IReadOnlyList<CopilotStreamDelta> Drain()
            {
                var batch = new CopilotStreamDelta[_segments.Count];
                for (var index = 0; index < _segments.Count; index++)
                    batch[index] = _segments[index].ToDelta();
                _segments.Clear();
                Size = 0;
                return batch;
            }
        }

        private sealed class PendingSegment
        {
            private readonly StringBuilder _reasoning = new();
            private readonly StringBuilder _content = new();

            public PendingSegment(CopilotStreamDelta delta)
            {
                HasReasoning = delta.HasReasoning;
                HasContent = delta.HasContent;
                Append(delta);
            }

            public bool HasReasoning { get; }

            public bool HasContent { get; }

            public void Append(CopilotStreamDelta delta)
            {
                if (delta.ReasoningContent != null)
                    _reasoning.Append(delta.ReasoningContent);
                if (delta.Content != null)
                    _content.Append(delta.Content);
            }

            public bool CanAppend(CopilotStreamDelta delta) =>
                HasReasoning != HasContent
                && HasReasoning == delta.HasReasoning
                && HasContent == delta.HasContent;

            public CopilotStreamDelta ToDelta() => new(_reasoning.ToString(), _content.ToString());
        }
    }
}
