using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotCheckpointingChatHistoryProvider : ChatHistoryProvider
    {
        private readonly InMemoryChatHistoryProvider _inner;
        private readonly Func<AIAgent, AgentSession, CancellationToken, ValueTask> _checkpointStored;

        public CopilotCheckpointingChatHistoryProvider(
            InMemoryChatHistoryProviderOptions options,
            Func<AIAgent, AgentSession, CancellationToken, ValueTask> checkpointStored)
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
                await _checkpointStored(context.Agent, context.Session, cancellationToken);
        }

        public override object? GetService(Type serviceType, object? serviceKey = null)
        {
            return serviceType.IsInstanceOfType(this)
                ? this
                : _inner.GetService(serviceType, serviceKey) ?? base.GetService(serviceType, serviceKey);
        }
    }
}
