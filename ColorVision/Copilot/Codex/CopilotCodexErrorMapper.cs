using System.Text.Json.Nodes;

namespace ColorVision.Copilot
{
    internal static class CopilotCodexErrorMapper
    {
        public static CopilotProviderPayloadException CreateException(JsonNode? error)
        {
            var info = (error as JsonObject)?["codexErrorInfo"];
            if (info is JsonValue value && value.TryGetValue<string>(out var code) && code.Length <= 64)
            {
                return code.ToLowerInvariant() switch
                {
                    "contextwindowexceeded" => Create("context_length_exceeded", "本机 Codex 的输入超过了模型上下文窗口。", false),
                    "sessionbudgetexceeded" => Create("insufficient_quota", "本机 Codex 的会话预算已用尽。请调整预算后重试。", false),
                    "usagelimitexceeded" => Create("insufficient_quota", "本机 Codex 的使用额度已用尽。请检查账户额度后重试。", false),
                    "ratelimitexceeded" => Create("rate_limit_exceeded", "本机 Codex 请求过于频繁，请稍后重试。", true),
                    "flexunavailable" => Create("service_unavailable", "本机 Codex 所用服务暂时不可用，请稍后重试。", true),
                    "serveroverloaded" => Create("overloaded_error", "本机 Codex 所用服务繁忙，请稍后重试。", true),
                    "internalservererror" => Create("server_error", "本机 Codex 所用服务出现暂时性错误，请稍后重试。", true),
                    "unauthorized" => Create("authentication_error", "本机 Codex 登录已失效，请重新登录后重试。", false),
                    "badrequest" => Create("invalid_request_error", "本机 Codex 拒绝了请求，请检查模型和请求设置。", false),
                    "cyberpolicy" or "misalignmentpolicyviolation" or "toomanydenials" =>
                        Create("permission_error", "本机 Codex 的安全或权限策略阻止了本次请求。", false),
                    _ => Unknown(),
                };
            }

            if (info is JsonObject variants && variants.Count == 1)
            {
                foreach (var variant in variants)
                {
                    if (variant.Key.Length > 64 || variant.Key.ToLowerInvariant() is not
                        ("httpconnectionfailed" or "responsestreamconnectionfailed" or "responsestreamdisconnected" or "responsetoomanyfailedattempts")
                        || variant.Value is not JsonObject details)
                    {
                        return Unknown();
                    }

                    // Never classify from the provider's message or additional details, which may contain secrets.
                    if (details["httpStatusCode"] == null)
                        return Create("connection_error", "本机 Codex 与模型服务的连接中断，请重试。", true);
                    if (details["httpStatusCode"] is not JsonValue statusValue || !statusValue.TryGetValue<int>(out var status))
                        return Unknown();
                    return status switch
                    {
                        0 => Create("connection_error", "本机 Codex 与模型服务的连接中断，请重试。", true),
                        401 => Create("authentication_error", "本机 Codex 登录已失效，请重新登录后重试。", false),
                        403 => Create("permission_error", "本机 Codex 无权访问所选模型，请检查账户权限。", false),
                        408 => Create("timeout_error", "本机 Codex 的模型请求超时，请重试。", true),
                        429 => Create("rate_limit_exceeded", "本机 Codex 请求过于频繁，请稍后重试。", true),
                        >= 500 and <= 599 => Create("server_error", "本机 Codex 所用服务出现暂时性错误，请稍后重试。", true),
                        _ => Create("invalid_request_error", "本机 Codex 的模型请求失败，请检查模型和服务设置。", false),
                    };
                }
            }
            return Unknown();
        }

        private static CopilotProviderPayloadException Unknown() =>
            Create("codex_error", "本机 Codex 未完成本轮回复，请检查登录、额度和模型设置。", false);

        private static CopilotProviderPayloadException Create(string code, string message, bool transient)
        {
            var exception = new CopilotProviderPayloadException(message, code, transient, string.Empty);
            CopilotProviderErrorPolicy.PreservePayloadError(exception, code, string.Empty);
            return exception;
        }
    }
}
