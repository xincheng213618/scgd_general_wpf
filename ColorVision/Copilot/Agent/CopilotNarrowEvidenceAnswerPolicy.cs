using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    internal static class CopilotNarrowEvidenceAnswerPolicy
    {
        private static readonly string[] ChineseFindingMarkers =
        {
            "审计结果", "可验证的问题", "问题", "缺陷", "风险", "发现",
        };

        private static readonly string[] NoFindingMarkers =
        {
            "未发现可验证", "没有发现可验证", "未形成可验证", "没有形成可验证",
            "证据不足以形成", "未作为缺陷报告", "未能验证", "无法验证",
            "no verified finding", "no verifiable finding", "did not establish",
            "could not verify", "unable to verify", "not being reported as a defect",
        };

        private static readonly string[] ChineseSpeculationMarkers =
        {
            "可能", "也许", "或许", "推测", "假设",
        };

        private static readonly string[] MissingEvidenceMarkers =
        {
            "未观察到", "未查看", "尚未检查", "需要检查", "需要确认", "需检查", "需确认",
            "not observed", "not inspected", "not examined", "needs verification",
            "requires verification", "need to inspect", "need to verify", "verify by checking",
        };

        private static readonly Regex EnglishFindingRegex = new(
            @"\b(?:finding|issue|defect|risk|problem)s?\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        private static readonly Regex EnglishSpeculationRegex = new(
            @"\b(?:may|might|could|possibly|potentially|speculative|hypothetical)\b",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static bool TryGetUnsupportedFindingReason(
            CopilotAgentRequest? request,
            string? answer,
            out string reason)
        {
            reason = string.Empty;
            var text = answer?.Trim() ?? string.Empty;
            if (!CopilotAgentRunBudget.TryGetNarrowEvidenceResultLimit(request, out _)
                || text.Length == 0
                || ContainsAny(text, NoFindingMarkers)
                || !ContainsFindingClaim(text))
            {
                return false;
            }

            if (ContainsAny(text, MissingEvidenceMarkers))
            {
                reason = "the answer says required evidence was not inspected";
                return true;
            }

            if (ContainsAny(text, ChineseSpeculationMarkers)
                || EnglishSpeculationRegex.IsMatch(text))
            {
                reason = "the claimed impact is speculative";
                return true;
            }

            return false;
        }

        public static string BuildNoVerifiedFindingAnswer(CopilotAgentRequest? request)
        {
            return ContainsChinese(request?.UserText)
                ? "本轮收集的证据不足以形成满足“可验证”标准的问题。候选结论依赖未读取的实现或推测性影响，因此未作为缺陷报告。"
                : "This run did not establish a verified finding. The candidate conclusion depended on uninspected implementation or speculative impact, so it is not being reported as a defect.";
        }

        private static bool ContainsFindingClaim(string text)
        {
            return ContainsAny(text, ChineseFindingMarkers) || EnglishFindingRegex.IsMatch(text);
        }

        private static bool ContainsAny(string text, string[] markers)
        {
            return Array.Exists(
                markers,
                marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsChinese(string? text)
        {
            return !string.IsNullOrEmpty(text)
                && text.Any(character => character is >= '\u3400' and <= '\u9fff');
        }
    }
}
