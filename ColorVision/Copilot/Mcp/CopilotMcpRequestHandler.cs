#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpRequestHandler
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
        };

        private readonly Func<CopilotMcpRuntimeSettings> _settingsProvider;
        private readonly CopilotMcpToolDispatcher _toolDispatcher;

        public CopilotMcpRequestHandler(Func<CopilotMcpRuntimeSettings> settingsProvider, CopilotMcpToolDispatcher? toolDispatcher = null)
        {
            _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
            _toolDispatcher = toolDispatcher ?? new CopilotMcpToolDispatcher();
        }

        public async Task<CopilotMcpHttpResponse> HandleAsync(CopilotMcpHttpRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var settings = _settingsProvider();
            if (!settings.Enabled)
                return JsonErrorResponse(503, null, -32000, "ColorVision MCP server is disabled.");

            if (!string.Equals(request.Path, "/mcp", StringComparison.OrdinalIgnoreCase))
                return JsonErrorResponse(404, null, -32004, "The requested MCP endpoint was not found.");

            if (!IsAuthorized(request.Headers, settings.BearerToken, out var authorizationFailureReason))
            {
                CopilotMcpAuditLogger.AuthenticationFailed(request.CallerSource, authorizationFailureReason);
                return JsonErrorResponse(401, null, -32001, "Unauthorized ColorVision MCP request.", new Dictionary<string, string>
                {
                    ["WWW-Authenticate"] = "Bearer",
                });
            }

            if (string.Equals(request.Method, "GET", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotMcpHttpResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(new
                    {
                        status = "ColorVision MCP endpoint is running.",
                        endpoint = settings.Endpoint,
                        transport = "streamable_http",
                    }, JsonOptions),
                };
            }

            if (!string.Equals(request.Method, "POST", StringComparison.OrdinalIgnoreCase))
                return JsonErrorResponse(405, null, -32005, "ColorVision MCP accepts GET and POST requests only.");

            if (string.IsNullOrWhiteSpace(request.Body))
                return JsonErrorResponse(400, null, -32700, "The JSON-RPC request body is empty.");

            try
            {
                using var document = JsonDocument.Parse(request.Body);
                if (document.RootElement.ValueKind == JsonValueKind.Array)
                    return await HandleBatchAsync(document.RootElement, request.CallerSource, cancellationToken);

                var response = await HandleJsonRpcAsync(document.RootElement, request.CallerSource, cancellationToken);
                if (response == null)
                    return new CopilotMcpHttpResponse { StatusCode = 202, Body = string.Empty };

                return new CopilotMcpHttpResponse
                {
                    StatusCode = 200,
                    Body = JsonSerializer.Serialize(response, JsonOptions),
                };
            }
            catch (JsonException ex)
            {
                return JsonErrorResponse(400, null, -32700, $"The JSON-RPC request body is invalid: {ex.Message}");
            }
        }

        private async Task<CopilotMcpHttpResponse> HandleBatchAsync(JsonElement root, string callerSource, CancellationToken cancellationToken)
        {
            if (root.GetArrayLength() == 0)
                return JsonErrorResponse(400, null, -32600, "A JSON-RPC batch request must not be empty.");

            var responses = new List<object>();
            foreach (var item in root.EnumerateArray())
            {
                var response = await HandleJsonRpcAsync(item, callerSource, cancellationToken);
                if (response != null)
                    responses.Add(response);
            }

            if (responses.Count == 0)
                return new CopilotMcpHttpResponse { StatusCode = 202, Body = string.Empty };

            return new CopilotMcpHttpResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(responses, JsonOptions),
            };
        }

        private async Task<object?> HandleJsonRpcAsync(JsonElement request, string callerSource, CancellationToken cancellationToken)
        {
            if (request.ValueKind != JsonValueKind.Object)
                return JsonRpcError(null, -32600, "A JSON-RPC request must be an object.");

            var id = ReadId(request);
            var hasId = request.TryGetProperty("id", out _);
            var method = request.TryGetProperty("method", out var methodElement) && methodElement.ValueKind == JsonValueKind.String
                ? methodElement.GetString() ?? string.Empty
                : string.Empty;

            if (string.IsNullOrWhiteSpace(method))
                return JsonRpcError(id, -32600, "A JSON-RPC request must include a method string.");

            if (string.Equals(method, "notifications/initialized", StringComparison.OrdinalIgnoreCase))
                return hasId ? JsonRpcResult(id, new { }) : null;

            return method switch
            {
                "initialize" => JsonRpcResult(id, BuildInitializeResult()),
                "ping" => JsonRpcResult(id, new { }),
                "tools/list" => JsonRpcResult(id, new { tools = _toolDispatcher.ListTools() }),
                "tools/call" => await HandleToolCallAsync(id, request, callerSource, cancellationToken),
                "resources/list" => JsonRpcResult(id, new { resources = _toolDispatcher.ListResources() }),
                "resources/read" => await HandleResourceReadAsync(id, request, cancellationToken),
                _ => JsonRpcError(id, -32601, $"Unknown MCP method: {method}"),
            };
        }

        private async Task<object> HandleToolCallAsync(object? id, JsonElement request, string callerSource, CancellationToken cancellationToken)
        {
            if (!request.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
                return JsonRpcError(id, -32602, "The tools/call method requires an object params value.");

            var toolName = paramsElement.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(toolName))
                return JsonRpcError(id, -32602, "The tools/call method requires a non-empty tool name.");

            var arguments = ReadArguments(paramsElement);
            var result = await _toolDispatcher.CallAsync(toolName, arguments, cancellationToken, callerSource);
            return JsonRpcResult(id, new
            {
                content = new[]
                {
                    new { type = "text", text = result.Text },
                },
                isError = !result.Success,
            });
        }

        private async Task<object> HandleResourceReadAsync(object? id, JsonElement request, CancellationToken cancellationToken)
        {
            if (!request.TryGetProperty("params", out var paramsElement) || paramsElement.ValueKind != JsonValueKind.Object)
                return JsonRpcError(id, -32602, "The resources/read method requires an object params value.");

            var uri = paramsElement.TryGetProperty("uri", out var uriElement) && uriElement.ValueKind == JsonValueKind.String
                ? uriElement.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(uri))
                return JsonRpcError(id, -32602, "The resources/read method requires a non-empty uri value.");

            var result = await _toolDispatcher.ReadResourceAsync(uri, cancellationToken);
            if (!result.Success)
                return JsonRpcError(id, -32002, result.Text);

            return JsonRpcResult(id, new
            {
                contents = new[]
                {
                    new
                    {
                        uri,
                        mimeType = "text/plain",
                        text = result.Text,
                    },
                },
            });
        }

        private static IReadOnlyDictionary<string, JsonElement> ReadArguments(JsonElement paramsElement)
        {
            if (!paramsElement.TryGetProperty("arguments", out var argumentsElement) || argumentsElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            return argumentsElement.EnumerateObject()
                .ToDictionary(property => property.Name, property => property.Value.Clone(), StringComparer.OrdinalIgnoreCase);
        }

        private static object BuildInitializeResult()
        {
            return new
            {
                protocolVersion = "2025-03-26",
                capabilities = new
                {
                    tools = new { },
                    resources = new { },
                },
                serverInfo = new
                {
                    name = "colorvision-mcp",
                    version = "1.0.0",
                },
            };
        }

        private static bool IsAuthorized(IReadOnlyDictionary<string, string> headers, string token, out string failureReason)
        {
            failureReason = string.Empty;
            if (string.IsNullOrWhiteSpace(token))
            {
                failureReason = "server token missing";
                return false;
            }

            if (!headers.TryGetValue("Authorization", out var value))
            {
                failureReason = "missing bearer token";
                return false;
            }

            if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                failureReason = "invalid authorization scheme";
                return false;
            }

            var expected = "Bearer " + token.Trim();
            if (string.Equals(value.Trim(), expected, StringComparison.Ordinal))
                return true;

            failureReason = "invalid bearer token";
            return false;
        }

        private static object? ReadId(JsonElement request)
        {
            if (!request.TryGetProperty("id", out var id))
                return null;

            return id.ValueKind switch
            {
                JsonValueKind.String => id.GetString(),
                JsonValueKind.Number when id.TryGetInt64(out var number) => number,
                JsonValueKind.Number => id.GetDouble(),
                JsonValueKind.Null => null,
                _ => id.ToString(),
            };
        }

        private static object JsonRpcResult(object? id, object result)
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                result,
            };
        }

        private static object JsonRpcError(object? id, int code, string message)
        {
            return new
            {
                jsonrpc = "2.0",
                id,
                error = new
                {
                    code,
                    message,
                },
            };
        }

        private static CopilotMcpHttpResponse JsonErrorResponse(int statusCode, object? id, int code, string message, IReadOnlyDictionary<string, string>? headers = null)
        {
            return new CopilotMcpHttpResponse
            {
                StatusCode = statusCode,
                Headers = headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Body = JsonSerializer.Serialize(JsonRpcError(id, code, message), JsonOptions),
            };
        }
    }
}