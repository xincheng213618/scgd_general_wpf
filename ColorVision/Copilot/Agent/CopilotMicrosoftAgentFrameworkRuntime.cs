#pragma warning disable MAAI001
#pragma warning disable CA1859
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.ClientModel;

namespace ColorVision.Copilot
{
    public sealed class CopilotMicrosoftAgentFrameworkRuntime : ICopilotAgentRuntime
    {
        private static readonly string[] ExperimentalToolNames =
        {
            "SearchDocs",
            "SearchFiles",
            "GetRecentLog",
        };

        private readonly CopilotToolRegistry _toolRegistry;
        private readonly CopilotAgentContextBuilder _contextBuilder;
        private readonly Func<CopilotProfileConfig, IChatClient> _chatClientFactory;

        public CopilotMicrosoftAgentFrameworkRuntime(CopilotToolRegistry toolRegistry, CopilotAgentContextBuilder contextBuilder)
            : this(toolRegistry, contextBuilder, CreateChatClient)
        {
        }

        public CopilotMicrosoftAgentFrameworkRuntime(
            CopilotToolRegistry toolRegistry,
            CopilotAgentContextBuilder contextBuilder,
            Func<CopilotProfileConfig, IChatClient> chatClientFactory)
        {
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
            _chatClientFactory = chatClientFactory ?? throw new ArgumentNullException(nameof(chatClientFactory));
        }

        public async Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            if (!CopilotAgentRuntimeRouter.CanUseAgentFramework(request.Profile, out var unsupportedReason))
                throw new NotSupportedException(unsupportedReason);

            var emit = CreateEventEmitter(onEvent);
            emit(CopilotAgentEvent.Status("Agent Framework Harness is preparing a safe experimental run."));

            var availableTools = _toolRegistry.FindTools(request)
                .Where(tool => ExperimentalToolNames.Contains(tool.Name, StringComparer.OrdinalIgnoreCase))
                .ToArray();
            var bridge = new HarnessToolBridge(request, availableTools, emit);
            var frameworkTools = bridge.CreateFunctions();
            var preparedPrompt = _contextBuilder.BuildAnswerMessages(request, Array.Empty<CopilotAgentStepRecord>());

            using var chatClient = _chatClientFactory(request.Profile);
            var agent = chatClient.AsHarnessAgent(new HarnessAgentOptions
            {
                Name = "ColorVisionCopilotExperimental",
                HarnessInstructions = BuildHarnessInstructions(availableTools),
                MaxOutputTokens = request.Profile.MaxTokens,
                MaximumIterationsPerRequest = Math.Max(1, request.Profile.MaxToolRounds),
                DisableCompaction = true,
                DisableFileMemory = true,
                DisableFileAccess = true,
                DisableWebSearch = true,
                DisableTodoProvider = true,
                DisableAgentModeProvider = true,
                DisableAgentSkillsProvider = true,
                DisableToolAutoApproval = true,
                DisableOpenTelemetry = true,
                ChatOptions = new ChatOptions
                {
                    Instructions = request.Profile.EffectiveSystemPrompt,
                    MaxOutputTokens = request.Profile.MaxTokens,
                    Temperature = (float)request.Profile.Temperature,
                    Tools = frameworkTools,
                },
            });

            var usage = CopilotTokenUsage.Empty;
            var messages = preparedPrompt.Messages.Select(ToFrameworkMessage).ToArray();
            emit(CopilotAgentEvent.Status(frameworkTools.Count == 0
                ? "Agent Framework is generating an answer without experimental tools."
                : $"Agent Framework can use {frameworkTools.Count} guarded read-only tool(s)."));

            await foreach (var update in agent.RunStreamingAsync(messages, null, null, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();

                foreach (var usageContent in update.Contents.OfType<UsageContent>())
                    usage = usage.Add(ToCopilotUsage(usageContent.Details));

                if (!string.IsNullOrEmpty(update.Text))
                    emit(CopilotAgentEvent.AnswerDelta(update.Text));
            }

            emit(CopilotAgentEvent.Completed());
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedPrompt.PreparedUserMessageContent,
                StepRecords = bridge.StepRecords,
                Usage = usage,
            };
        }

        private static IChatClient CreateChatClient(CopilotProfileConfig profile)
        {
            var options = new OpenAIClientOptions
            {
                Endpoint = NormalizeOpenAiEndpoint(profile.BaseUrl),
            };
            var client = new ChatClient(profile.Model, new ApiKeyCredential(profile.ApiKey), options);
            return client.AsIChatClient();
        }

        private static Uri NormalizeOpenAiEndpoint(string baseUrl)
        {
            var value = (baseUrl ?? string.Empty).Trim().TrimEnd('/');
            const string chatCompletionsSuffix = "/chat/completions";
            if (value.EndsWith(chatCompletionsSuffix, StringComparison.OrdinalIgnoreCase))
                value = value[..^chatCompletionsSuffix.Length];

            var endpoint = new Uri(value, UriKind.Absolute);
            if (string.IsNullOrWhiteSpace(endpoint.AbsolutePath) || endpoint.AbsolutePath == "/")
                value = value.TrimEnd('/') + "/v1";

            return new Uri(value, UriKind.Absolute);
        }

        private static Microsoft.Extensions.AI.ChatMessage ToFrameworkMessage(CopilotRequestMessage message)
        {
            var role = message.Role?.Trim().ToLowerInvariant() switch
            {
                "assistant" => ChatRole.Assistant,
                "system" => ChatRole.System,
                _ => ChatRole.User,
            };
            return new Microsoft.Extensions.AI.ChatMessage(role, message.Content ?? string.Empty);
        }

        private static CopilotTokenUsage ToCopilotUsage(UsageDetails details)
        {
            static int ToInt(long? value) => value.HasValue ? (int)Math.Clamp(value.Value, 0, int.MaxValue) : 0;

            return new CopilotTokenUsage(
                ToInt(details.InputTokenCount),
                ToInt(details.OutputTokenCount),
                ToInt(details.TotalTokenCount));
        }

        private static Action<CopilotAgentEvent> CreateEventEmitter(Action<CopilotAgentEvent> onEvent)
        {
            var dispatcher = Application.Current?.Dispatcher;
            return agentEvent =>
            {
                if (dispatcher != null && !dispatcher.CheckAccess())
                {
                    dispatcher.Invoke(() => onEvent(agentEvent));
                    return;
                }

                onEvent(agentEvent);
            };
        }

        private static string BuildHarnessInstructions(IReadOnlyList<ICopilotTool> tools)
        {
            var builder = new StringBuilder();
            builder.AppendLine("You are the experimental ColorVision Agent runtime.");
            builder.AppendLine("Use only the guarded read-only functions supplied for this run. Never claim a tool succeeded unless its returned result says success.");
            builder.AppendLine("Do not attempt file writes, shell execution, device control, flow execution, settings changes, or other mutations.");
            builder.AppendLine("Call a function only when it adds evidence needed to answer the user, avoid repeating identical calls, and then answer naturally.");

            if (tools.Count > 0)
            {
                builder.AppendLine("Available ColorVision functions:");
                foreach (var tool in tools)
                    builder.Append("- ").Append(tool.Name).Append(": ").AppendLine(tool.Description);
            }

            return builder.ToString().TrimEnd();
        }

        private sealed class HarnessToolBridge
        {
            private readonly CopilotAgentRequest _request;
            private readonly IReadOnlyDictionary<string, ICopilotTool> _tools;
            private readonly Action<CopilotAgentEvent> _emit;
            private readonly List<CopilotAgentStepRecord> _stepRecords = new();
            private readonly object _syncRoot = new();

            public HarnessToolBridge(CopilotAgentRequest request, IReadOnlyList<ICopilotTool> tools, Action<CopilotAgentEvent> emit)
            {
                _request = request;
                _tools = tools.ToDictionary(tool => tool.Name, StringComparer.OrdinalIgnoreCase);
                _emit = emit;
            }

            public IReadOnlyList<CopilotAgentStepRecord> StepRecords
            {
                get
                {
                    lock (_syncRoot)
                        return _stepRecords.ToArray();
                }
            }

            public IList<AITool> CreateFunctions()
            {
                var functions = new List<AITool>();
                if (_tools.ContainsKey("SearchDocs"))
                {
                    functions.Add(AIFunctionFactory.Create(
                        new Func<string, CancellationToken, Task<string>>(SearchDocsAsync),
                        "search_colorvision_docs",
                        "Search ColorVision documentation for a focused query and return relevant documented evidence.",
                        null));
                }

                if (_tools.ContainsKey("SearchFiles"))
                {
                    functions.Add(AIFunctionFactory.Create(
                        new Func<string, CancellationToken, Task<string>>(SearchFilesAsync),
                        "search_colorvision_files",
                        "Search allowed ColorVision workspace file names and relative paths for a focused query.",
                        null));
                }

                if (_tools.ContainsKey("GetRecentLog"))
                {
                    functions.Add(AIFunctionFactory.Create(
                        new Func<string, CancellationToken, Task<string>>(GetRecentLogAsync),
                        "get_recent_colorvision_log",
                        "Read recent ColorVision application log lines, optionally filtered by a short query.",
                        null));
                }

                return functions;
            }

            private Task<string> SearchDocsAsync(string query, CancellationToken cancellationToken) => ExecuteAsync("SearchDocs", query, cancellationToken);

            private Task<string> SearchFilesAsync(string query, CancellationToken cancellationToken) => ExecuteAsync("SearchFiles", query, cancellationToken);

            private Task<string> GetRecentLogAsync(string query, CancellationToken cancellationToken) => ExecuteAsync("GetRecentLog", query, cancellationToken);

            private async Task<string> ExecuteAsync(string toolName, string query, CancellationToken cancellationToken)
            {
                if (!_tools.TryGetValue(toolName, out var tool))
                    return $"Tool unavailable: {toolName}.";

                _emit(CopilotAgentEvent.Status($"Agent Framework is calling {toolName}."));
                var toolInput = new CopilotAgentToolInput { Query = query?.Trim() ?? string.Empty };
                CopilotToolResult result;
                try
                {
                    result = await tool.ExecuteAsync(_request, toolInput, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new CopilotToolResult
                    {
                        ToolName = toolName,
                        Success = false,
                        Summary = $"{toolName} execution failed.",
                        ErrorMessage = ex.Message,
                    };
                }

                lock (_syncRoot)
                {
                    _stepRecords.Add(new CopilotAgentStepRecord
                    {
                        Round = _stepRecords.Count + 1,
                        ToolCall = new CopilotToolCall
                        {
                            ToolName = toolName,
                            ToolInput = toolInput,
                            Reason = "Selected by Microsoft Agent Framework Harness.",
                        },
                        Observation = CopilotToolObservation.FromResult(result),
                    });
                }

                _emit(CopilotAgentEvent.FromToolResult(result));
                return FormatToolResult(result);
            }

            private static string FormatToolResult(CopilotToolResult result)
            {
                var builder = new StringBuilder();
                builder.AppendLine(result.Success ? "success: true" : "success: false");
                if (!string.IsNullOrWhiteSpace(result.Summary))
                    builder.Append("summary: ").AppendLine(result.Summary.Trim());
                if (!string.IsNullOrWhiteSpace(result.Content))
                    builder.AppendLine("content:").AppendLine(result.Content.Trim());
                if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                    builder.Append("error: ").AppendLine(result.ErrorMessage.Trim());
                return builder.ToString().TrimEnd();
            }
        }
    }
}
