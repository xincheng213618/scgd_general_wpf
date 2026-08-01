#pragma warning disable MAAI001
using Microsoft.Extensions.AI;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal sealed partial class HarnessToolBridge
        {
            internal sealed class UserQuestionAIFunction : AIFunction
            {
                private static readonly JsonSerializerOptions SerializerOptions = new()
                {
                    PropertyNameCaseInsensitive = true,
                };
                private static readonly JsonElement Schema = JsonDocument.Parse(
                    """
                    {
                      "type": "object",
                      "properties": {
                        "header": {
                          "type": "string",
                          "description": "Short UI label, 1-12 characters.",
                          "minLength": 1,
                          "maxLength": 12
                        },
                        "question": {
                          "type": "string",
                          "description": "One concise clarification question whose answer materially changes the outcome.",
                          "minLength": 1,
                          "maxLength": 500
                        },
                        "options": {
                          "type": "array",
                          "description": "Two or three mutually exclusive choices. Put the recommended choice first and suffix its label with '(Recommended)'.",
                          "minItems": 2,
                          "maxItems": 3,
                          "items": {
                            "type": "object",
                            "properties": {
                              "label": {
                                "type": "string",
                                "description": "Short choice label.",
                                "minLength": 1,
                                "maxLength": 80
                              },
                              "description": {
                                "type": "string",
                                "description": "One short sentence explaining the impact or tradeoff.",
                                "maxLength": 240
                              }
                            },
                            "required": ["label", "description"],
                            "additionalProperties": false
                          }
                        }
                      },
                      "required": ["header", "question", "options"],
                      "additionalProperties": false
                    }
                    """).RootElement.Clone();

                private readonly CopilotUserQuestionCoordinator _coordinator;
                private readonly CopilotAgentRequest _request;
                private readonly Action<CopilotAgentEvent> _emit;

                public UserQuestionAIFunction(
                    CopilotUserQuestionCoordinator coordinator,
                    CopilotAgentRequest request,
                    Action<CopilotAgentEvent> emit)
                {
                    _coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
                    _request = request ?? throw new ArgumentNullException(nameof(request));
                    _emit = emit ?? throw new ArgumentNullException(nameof(emit));
                }

                public override string Name => "AskUserQuestion";

                public override string Description =>
                    "Pause the current main Agent task to ask one structured clarification question. "
                    + "Use only when 2-3 materially different valid choices remain; this is not approval. "
                    + "Call this function alone in a provider response. "
                    + "The user may select an option or type a different answer.";

                public override JsonElement JsonSchema => Schema;

                protected override async ValueTask<object?> InvokeCoreAsync(
                    AIFunctionArguments arguments,
                    CancellationToken cancellationToken)
                {
                    CopilotUserQuestionInput? input;
                    try
                    {
                        input = JsonSerializer.Deserialize<CopilotUserQuestionInput>(
                            JsonSerializer.Serialize(arguments),
                            SerializerOptions);
                    }
                    catch (JsonException ex)
                    {
                        return FormatRejected("The structured question arguments are invalid: " + ex.Message);
                    }

                    try
                    {
                        var resolved = await _coordinator.AskAsync(
                            _request,
                            input ?? new CopilotUserQuestionInput(),
                            _emit,
                            cancellationToken).ConfigureAwait(false);
                        return JsonSerializer.Serialize(new
                        {
                            outcome = "answered",
                            answer = resolved.Answer,
                        });
                    }
                    catch (ArgumentException ex)
                    {
                        return FormatRejected(ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        return FormatRejected(ex.Message);
                    }
                }

                private static string FormatRejected(string error)
                {
                    return JsonSerializer.Serialize(new
                    {
                        outcome = "rejected",
                        error = CopilotUserFacingErrorFormatter.Sanitize(error),
                    });
                }
            }
        }
    }
}
