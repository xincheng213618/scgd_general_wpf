using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotProviderToolHistoryEntryKind
    {
        Call,
        Result,
    }

    internal readonly record struct CopilotProviderToolHistoryEntry(
        CopilotProviderToolHistoryEntryKind Kind,
        string CallId,
        string ToolName);

    internal sealed class CopilotProviderToolHistoryDelta
    {
        private CopilotProviderToolHistoryDelta(
            IReadOnlyList<CopilotProviderToolHistoryEntry> entries)
        {
            Entries = entries;
        }

        public IReadOnlyList<CopilotProviderToolHistoryEntry> Entries { get; }

        public static CopilotProviderToolHistoryDelta Capture(
            IEnumerable<ChatMessage>? requestMessages,
            IEnumerable<ChatMessage>? responseMessages)
        {
            var entries = new List<CopilotProviderToolHistoryEntry>();
            var observed = new HashSet<(CopilotProviderToolHistoryEntryKind Kind, string CallId)>();
            CaptureResults(requestMessages, entries, observed);
            CaptureCalls(responseMessages, entries, observed);
            CaptureResults(responseMessages, entries, observed);
            return new CopilotProviderToolHistoryDelta(entries.ToArray());
        }

        private static void CaptureCalls(
            IEnumerable<ChatMessage>? messages,
            List<CopilotProviderToolHistoryEntry> entries,
            HashSet<(CopilotProviderToolHistoryEntryKind Kind, string CallId)> observed)
        {
            foreach (var call in SelectContents(messages).OfType<FunctionCallContent>())
            {
                var callId = (call.CallId ?? string.Empty).Trim();
                var toolName = (call.Name ?? string.Empty).Trim();
                var key = (CopilotProviderToolHistoryEntryKind.Call, callId);
                if (!call.InformationalOnly
                    && callId.Length > 0
                    && toolName.Length > 0
                    && observed.Add(key))
                {
                    entries.Add(new CopilotProviderToolHistoryEntry(key.Item1, callId, toolName));
                }
            }
        }

        private static void CaptureResults(
            IEnumerable<ChatMessage>? messages,
            List<CopilotProviderToolHistoryEntry> entries,
            HashSet<(CopilotProviderToolHistoryEntryKind Kind, string CallId)> observed)
        {
            foreach (var result in SelectContents(messages).OfType<FunctionResultContent>())
            {
                var callId = (result.CallId ?? string.Empty).Trim();
                var key = (CopilotProviderToolHistoryEntryKind.Result, callId);
                if (callId.Length > 0 && observed.Add(key))
                    entries.Add(new CopilotProviderToolHistoryEntry(key.Item1, callId, string.Empty));
            }
        }

        private static IEnumerable<AIContent> SelectContents(
            IEnumerable<ChatMessage>? messages)
        {
            return (messages ?? Array.Empty<ChatMessage>())
                .Where(message => message != null)
                .SelectMany(message => message.Contents ?? Array.Empty<AIContent>());
        }
    }

    internal sealed class CopilotCheckpointingChatHistoryProvider : ChatHistoryProvider
    {
        private readonly InMemoryChatHistoryProvider _inner;
        private readonly Func<AIAgent, AgentSession, CopilotProviderToolHistoryDelta, CancellationToken, ValueTask> _checkpointStored;

        public CopilotCheckpointingChatHistoryProvider(
            InMemoryChatHistoryProviderOptions options,
            Func<AIAgent, AgentSession, CopilotProviderToolHistoryDelta, CancellationToken, ValueTask> checkpointStored)
        {
            _inner = new InMemoryChatHistoryProvider(options ?? throw new ArgumentNullException(nameof(options)));
            _checkpointStored = checkpointStored ?? throw new ArgumentNullException(nameof(checkpointStored));
        }

        public override IReadOnlyList<string> StateKeys => _inner.StateKeys;

        protected override ValueTask<IEnumerable<ChatMessage>> InvokingCoreAsync(
            InvokingContext context,
            CancellationToken cancellationToken)
        {
            return _inner.InvokingAsync(context, cancellationToken);
        }

        protected override async ValueTask InvokedCoreAsync(
            InvokedContext context,
            CancellationToken cancellationToken)
        {
            await _inner.InvokedAsync(context, cancellationToken);
            if (context.InvokeException == null)
            {
                var toolHistoryDelta = CopilotProviderToolHistoryDelta.Capture(
                    context.RequestMessages,
                    context.ResponseMessages);
                await _checkpointStored(
                    context.Agent,
                    context.Session,
                    toolHistoryDelta,
                    cancellationToken);
            }
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType.IsInstanceOfType(this)
                ? this
                : _inner.GetService(serviceType, serviceKey) ?? base.GetService(serviceType, serviceKey);
        }
    }
}
