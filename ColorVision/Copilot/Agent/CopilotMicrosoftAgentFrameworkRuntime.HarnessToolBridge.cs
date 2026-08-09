#pragma warning disable MAAI001
#pragma warning disable CA1859
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
            private readonly CopilotAgentRequest _request;
            private readonly CopilotExecutionScope _executionScope;
            private readonly IReadOnlyDictionary<string, ICopilotTool> _tools;
            private readonly CopilotToolExecutor _toolExecutor;
            private readonly CopilotFrameworkApprovalCoordinator _approvalCoordinator;
            private readonly Action<CopilotAgentEvent> _emit;
            private readonly Func<long> _capabilityRevisionProvider;
            private readonly List<CopilotAgentStepRecord> _stepRecords = new();
            private readonly Dictionary<string, ToolAttemptState> _attemptsBySignature = new(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<CopilotFrameworkApprovalReservationKey, FrameworkApprovalReservation> _approvedCalls = new();
            private readonly CopilotProviderToolCallLedger _providerToolCalls = new();
            private readonly object _syncRoot = new();
            private readonly int _maxToolCalls;
            private readonly Action<CopilotDelegatedRunUsage>? _recordDelegatedRunUsage;
            private readonly CopilotAgentToolBudgetCompletionGate _toolBudgetCompletionGate;
            private CopilotTokenUsage _delegatedUsage;
            private int _reservedToolCalls;
            private MessageInjectingChatClient? _messageInjector;
            private AgentSession? _messageInjectionSession;

            public HarnessToolBridge(
                CopilotAgentRequest request,
                CopilotExecutionScope executionScope,
                IReadOnlyList<ICopilotTool> tools,
                int maxToolCalls,
                CopilotToolExecutor toolExecutor,
                CopilotFrameworkApprovalCoordinator approvalCoordinator,
                Action<CopilotAgentEvent> emit,
                Func<long> capabilityRevisionProvider,
                Action<CopilotDelegatedRunUsage>? recordDelegatedRunUsage = null,
                Action? onToolBudgetExhausted = null)
            {
                _request = request;
                _executionScope = executionScope ?? throw new ArgumentNullException(nameof(executionScope));
                _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
                _maxToolCalls = Math.Max(1, maxToolCalls);
                _toolExecutor = toolExecutor;
                _approvalCoordinator = approvalCoordinator;
                _emit = emit;
                _capabilityRevisionProvider = capabilityRevisionProvider ?? throw new ArgumentNullException(nameof(capabilityRevisionProvider));
                _recordDelegatedRunUsage = recordDelegatedRunUsage;
                _toolBudgetCompletionGate = new CopilotAgentToolBudgetCompletionGate(onToolBudgetExhausted);
            }

            public IReadOnlyList<CopilotAgentStepRecord> StepRecords
            {
                get
                {
                    lock (_syncRoot)
                        return _stepRecords.OrderBy(step => step.Round).ToArray();
                }
            }

            public bool ToolBudgetExhausted
            {
                get => _toolBudgetCompletionGate.IsExhausted;
            }

            public CopilotTokenUsage DelegatedUsage
            {
                get
                {
                    lock (_syncRoot)
                        return _delegatedUsage;
                }
            }

            public IList<AITool> CreateFunctions()
            {
                var functions = new List<AITool>();
                foreach (var tool in _tools.Values)
                {
                    var function = new HarnessToolFunction(this, tool);
                    functions.Add(RequiresNativeApproval(tool) ? new ApprovalRequiredAIFunction(function) : function);
                }
                return functions;
            }

            public void AttachMessageInjection(
                MessageInjectingChatClient messageInjector,
                AgentSession session)
            {
                ArgumentNullException.ThrowIfNull(messageInjector);
                ArgumentNullException.ThrowIfNull(session);
                lock (_syncRoot)
                {
                    if (_messageInjector != null || _messageInjectionSession != null)
                        throw new InvalidOperationException("Hook message injection is already attached.");
                    _messageInjector = messageInjector;
                    _messageInjectionSession = session;
                }
            }

            internal static IReadOnlyList<ChatMessage> CreateHookAdditionalContextMessages(
                IReadOnlyList<string> contexts)
            {
                return (contexts ?? Array.Empty<string>())
                    .Where(context => !string.IsNullOrWhiteSpace(context))
                    .Select(context => new ChatMessage(new ChatRole("developer"), context))
                    .ToArray();
            }

            private async Task EnqueueHookAdditionalContextAsync(
                IReadOnlyList<string> contexts,
                CancellationToken cancellationToken)
            {
                var messages = CreateHookAdditionalContextMessages(contexts);
                if (messages.Count == 0)
                    return;

                MessageInjectingChatClient? messageInjector;
                AgentSession? session;
                lock (_syncRoot)
                {
                    messageInjector = _messageInjector;
                    session = _messageInjectionSession;
                }
                if (messageInjector == null || session == null)
                {
                    _emit(CopilotAgentEvent.RuntimeDiagnostic(
                        "Hook additional context could not be delivered because the Agent message-injection session was unavailable."));
                    return;
                }

                try
                {
                    await messageInjector.EnqueueMessagesAsync(
                        session,
                        messages,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _emit(CopilotAgentEvent.RuntimeDiagnostic(
                        $"Hook additional context could not be delivered to the Agent. ErrorType={ex.GetType().Name}"));
                }
            }

        }
    }
}
