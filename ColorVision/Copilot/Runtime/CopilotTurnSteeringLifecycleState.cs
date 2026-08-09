using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnSteeringLifecycleState
    {
        private readonly IReadOnlyDictionary<string, TerminalSteeringMessage> _messages;

        private CopilotTurnSteeringLifecycleState(
            IReadOnlyDictionary<string, TerminalSteeringMessage> messages)
        {
            _messages = messages;
        }

        public static CopilotTurnSteeringLifecycleState Empty { get; } = new(
            new Dictionary<string, TerminalSteeringMessage>(StringComparer.Ordinal));

        public int DeliveredCount => _messages.Values.Count(
            message => message.Disposition == SteeringDisposition.Delivered);

        public int RecoveredCount => _messages.Values.Count(
            message => message.Disposition == SteeringDisposition.Recovered);

        public CopilotTurnSteeringLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            var disposition = agentEvent.Type switch
            {
                CopilotAgentEventType.SteeringDelivered => SteeringDisposition.Delivered,
                CopilotAgentEventType.SteeringRecovery => SteeringDisposition.Recovered,
                _ => (SteeringDisposition?)null,
            };
            if (disposition == null)
                return this;

            var messages = new Dictionary<string, TerminalSteeringMessage>(
                _messages,
                StringComparer.Ordinal);
            foreach (var message in agentEvent.SteeringMessages)
            {
                if (messages.TryGetValue(message.MessageId, out var existing))
                {
                    if (existing.Disposition != disposition.Value)
                    {
                        throw new InvalidOperationException(
                            "Copilot Agent marked the same steering message as both delivered and recovered.");
                    }
                    throw new InvalidOperationException(
                        $"Copilot Agent marked the same steering message as {FormatDisposition(disposition.Value)} more than once.");
                }

                messages.Add(
                    message.MessageId,
                    new TerminalSteeringMessage(message.Text, disposition.Value));
            }
            return new CopilotTurnSteeringLifecycleState(messages);
        }

        private static string FormatDisposition(SteeringDisposition disposition) =>
            disposition == SteeringDisposition.Delivered ? "delivered" : "recovered";

        private sealed record TerminalSteeringMessage(
            string Text,
            SteeringDisposition Disposition);

        private enum SteeringDisposition
        {
            Delivered,
            Recovered,
        }
    }
}
