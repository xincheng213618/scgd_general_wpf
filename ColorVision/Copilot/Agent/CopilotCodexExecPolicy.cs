using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexExecPolicyDecision
    {
        NoMatch,
        Allow,
        Prompt,
        Forbidden,
    }

    internal sealed record CopilotCodexExecPolicyPatternElement(
        IReadOnlyList<string> Alternatives)
    {
        public bool IsStructurallyValid() => Alternatives.Count is > 0 and <= 16
            && Alternatives.All(alternative => !string.IsNullOrWhiteSpace(alternative)
                && alternative.Length <= 512
                && !alternative.Any(char.IsControl));

        public CopilotCodexExecPolicyPatternElement CreateSnapshot() =>
            new(Alternatives.ToArray());
    }

    internal sealed record CopilotCodexExecPolicyRule(
        string SourceFilePath,
        CopilotProjectInstructionConfigSources Source,
        IReadOnlyList<CopilotCodexExecPolicyPatternElement> Pattern,
        CopilotCodexExecPolicyDecision Decision,
        string Justification,
        int Order)
    {
        public bool IsStructurallyValid() => !string.IsNullOrWhiteSpace(SourceFilePath)
            && Enum.IsDefined(Source)
            && Source != CopilotProjectInstructionConfigSources.None
            && Pattern.Count is > 0 and <= 32
            && Pattern.All(element => element?.IsStructurallyValid() == true)
            && Decision is CopilotCodexExecPolicyDecision.Allow
                or CopilotCodexExecPolicyDecision.Prompt
                or CopilotCodexExecPolicyDecision.Forbidden
            && Justification.Length <= 2_048
            && !Justification.Any(char.IsControl)
            && Order >= 0;

        public bool Matches(IReadOnlyList<string> command)
        {
            if (command == null || command.Count < Pattern.Count)
                return false;
            for (var index = 0; index < Pattern.Count; index++)
            {
                if (!Pattern[index].Alternatives.Contains(command[index], StringComparer.Ordinal))
                    return false;
            }
            return true;
        }

        public CopilotCodexExecPolicyRule CreateSnapshot() => new(
            SourceFilePath,
            Source,
            Pattern.Select(element => element.CreateSnapshot()).ToArray(),
            Decision,
            Justification,
            Order);

        public string FormatPattern() => string.Join(
            " ",
            Pattern.Select(element => element.Alternatives.Count == 1
                ? element.Alternatives[0]
                : "{" + string.Join("|", element.Alternatives) + "}"));
    }

    internal sealed record CopilotCodexExecPolicyIssue(
        string SourceFilePath,
        string Message);

    internal sealed record CopilotCodexExecPolicyEvaluation(
        CopilotCodexExecPolicyDecision Decision,
        string Reason,
        IReadOnlyList<CopilotCodexExecPolicyRule> MatchedRules,
        IReadOnlyList<IReadOnlyList<string>> Commands)
    {
        public static CopilotCodexExecPolicyEvaluation NotApplicable { get; } = new(
            CopilotCodexExecPolicyDecision.NoMatch,
            string.Empty,
            Array.Empty<CopilotCodexExecPolicyRule>(),
            Array.Empty<IReadOnlyList<string>>());
    }

    internal static class CopilotCodexExecPolicyEvaluator
    {
        public static CopilotCodexExecPolicyEvaluation Evaluate(
            CopilotAgentRequest request,
            ICopilotTool tool,
            CopilotAgentToolInput input)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(tool);
            input ??= CopilotAgentToolInput.Empty;
            if (!string.Equals(tool.Name, "RunShellCommand", StringComparison.Ordinal)
                || request.CodexExecPolicyRules.Count == 0
                || !TryReadString(input, "command", out var commandText))
            {
                return CopilotCodexExecPolicyEvaluation.NotApplicable;
            }

            _ = TryReadString(input, "shell", out var shellText);
            var requestedShell = CopilotShellCommandService.TryParseShell(shellText, out var parsedShell)
                ? parsedShell
                : CopilotShellKind.Auto;
            var shell = CopilotShellCommandService.ResolveShell(
                requestedShell,
                request.PreferredShell);
            var parse = CopilotCodexShellCommandTokenizer.Parse(commandText, shell);
            if (parse.Commands.Count == 0)
                return CopilotCodexExecPolicyEvaluation.NotApplicable;

            var rules = request.CodexExecPolicyRules
                .Where(rule => rule?.IsStructurallyValid() == true)
                .OrderBy(rule => rule.Order)
                .ToArray();
            var matches = new List<(CopilotCodexExecPolicyRule Rule, IReadOnlyList<string> Command)>();
            var allCommandsExplicitlyAllowed = parse.IsReusableApprovalSafe;
            foreach (var command in parse.Commands)
            {
                var commandMatches = rules.Where(rule => rule.Matches(command)).ToArray();
                matches.AddRange(commandMatches.Select(rule => (rule, command)));
                if (!commandMatches.Any(rule => rule.Decision == CopilotCodexExecPolicyDecision.Allow))
                    allCommandsExplicitlyAllowed = false;
            }

            var matchedRules = matches.Select(match => match.Rule).Distinct().ToArray();
            var forbiddenMatches = matches
                .Where(match => match.Rule.Decision == CopilotCodexExecPolicyDecision.Forbidden)
                .OrderByDescending(match => match.Rule.Pattern.Count)
                .ThenBy(match => match.Rule.Order)
                .ToArray();
            if (forbiddenMatches.Length > 0)
            {
                var forbidden = forbiddenMatches[0];
                return new CopilotCodexExecPolicyEvaluation(
                    CopilotCodexExecPolicyDecision.Forbidden,
                    FormatDecisionReason(forbidden.Rule, forbidden.Command, "was rejected"),
                    matchedRules,
                    parse.Commands);
            }

            var promptMatches = matches
                .Where(match => match.Rule.Decision == CopilotCodexExecPolicyDecision.Prompt)
                .OrderByDescending(match => match.Rule.Pattern.Count)
                .ThenBy(match => match.Rule.Order)
                .ToArray();
            if (promptMatches.Length > 0)
            {
                var prompt = promptMatches[0];
                return new CopilotCodexExecPolicyEvaluation(
                    CopilotCodexExecPolicyDecision.Prompt,
                    FormatDecisionReason(prompt.Rule, prompt.Command, "requires approval"),
                    matchedRules,
                    parse.Commands);
            }

            if (allCommandsExplicitlyAllowed)
            {
                var allow = matches.Select(match => match.Rule)
                    .Where(rule => rule.Decision == CopilotCodexExecPolicyDecision.Allow)
                    .OrderByDescending(rule => rule.Pattern.Count)
                    .ThenBy(rule => rule.Order)
                    .First();
                var detail = string.IsNullOrWhiteSpace(allow.Justification)
                    ? $"Codex exec policy allows commands starting with `{allow.FormatPattern()}`."
                    : $"Codex exec policy allows this command: {allow.Justification}";
                return new CopilotCodexExecPolicyEvaluation(
                    CopilotCodexExecPolicyDecision.Allow,
                    detail,
                    matchedRules,
                    parse.Commands);
            }

            return new CopilotCodexExecPolicyEvaluation(
                CopilotCodexExecPolicyDecision.NoMatch,
                parse.IsReusableApprovalSafe
                    ? string.Empty
                    : "The shell expression contains dynamic or ambiguous syntax, so reusable exec-policy approval was not applied.",
                matchedRules,
                parse.Commands);
        }

        private static string FormatDecisionReason(
            CopilotCodexExecPolicyRule rule,
            IReadOnlyList<string> command,
            string action)
        {
            var renderedCommand = string.Join(" ", command);
            return string.IsNullOrWhiteSpace(rule.Justification)
                ? $"`{renderedCommand}` {action} by Codex exec policy rule `{rule.FormatPattern()}`."
                : $"`{renderedCommand}` {action} by Codex exec policy: {rule.Justification}";
        }

        private static bool TryReadString(
            CopilotAgentToolInput input,
            string name,
            out string value)
        {
            value = string.Empty;
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
                return false;
            if (raw is string text)
            {
                value = text.Trim();
                return value.Length > 0;
            }
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                value = (element.GetString() ?? string.Empty).Trim();
                return value.Length > 0;
            }
            return false;
        }
    }

    internal sealed record CopilotCodexShellCommandParseResult(
        IReadOnlyList<IReadOnlyList<string>> Commands,
        bool IsReusableApprovalSafe);

    internal static class CopilotCodexShellCommandTokenizer
    {
        public static CopilotCodexShellCommandParseResult Parse(
            string? commandText,
            CopilotShellKind shell)
        {
            var commands = new List<IReadOnlyList<string>>();
            var tokens = new List<string>();
            var token = new StringBuilder();
            var text = commandText ?? string.Empty;
            var quote = '\0';
            var safe = true;

            void CompleteToken()
            {
                if (token.Length == 0)
                    return;
                tokens.Add(token.ToString());
                token.Clear();
            }

            void CompleteCommand()
            {
                CompleteToken();
                if (tokens.Count == 0)
                    return;
                commands.Add(tokens.ToArray());
                tokens.Clear();
            }

            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        if (quote == '\''
                            && shell == CopilotShellKind.PowerShell
                            && index + 1 < text.Length
                            && text[index + 1] == '\'')
                        {
                            token.Append('\'');
                            index++;
                            continue;
                        }
                        quote = '\0';
                        continue;
                    }
                    if (shell == CopilotShellKind.PowerShell && current == '`')
                    {
                        if (index + 1 >= text.Length)
                        {
                            safe = false;
                            continue;
                        }
                        token.Append(text[++index]);
                        continue;
                    }
                    if (quote == '"' && current == '$')
                        safe = false;
                    token.Append(current);
                    continue;
                }

                if (current is '\'' or '"')
                {
                    quote = current;
                    continue;
                }
                if (current == '#'
                    && shell == CopilotShellKind.PowerShell
                    && token.Length == 0)
                {
                    while (index + 1 < text.Length
                        && text[index + 1] is not '\r' and not '\n')
                    {
                        index++;
                    }
                    continue;
                }
                if (char.IsWhiteSpace(current))
                {
                    if (current is '\r' or '\n')
                        CompleteCommand();
                    else
                        CompleteToken();
                    continue;
                }
                if (current == ';' || current == '|')
                {
                    CompleteCommand();
                    if (index + 1 < text.Length && text[index + 1] == current)
                        index++;
                    continue;
                }
                if (current == '&')
                {
                    CompleteCommand();
                    if (index + 1 < text.Length && text[index + 1] == '&')
                        index++;
                    else
                        safe = false;
                    continue;
                }
                if (current is '<' or '>' or '(' or ')' or '{' or '}')
                    safe = false;
                if (shell == CopilotShellKind.PowerShell && current is '$' or '@')
                    safe = false;
                if (shell == CopilotShellKind.CommandPrompt && current is '%' or '!' or '^')
                    safe = false;
                token.Append(current);
            }

            if (quote != '\0')
                safe = false;
            CompleteCommand();
            return new CopilotCodexShellCommandParseResult(commands.ToArray(), safe);
        }
    }
}
