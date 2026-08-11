using System;
using System.Collections.Generic;
using System.IO;
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

    internal sealed record CopilotCodexExecPolicyDiscoveryResult(
        IReadOnlyList<CopilotCodexExecPolicyRule> Rules,
        IReadOnlyList<CopilotCodexExecPolicyIssue> Issues,
        IReadOnlyList<string> SourceFilePaths)
    {
        public static CopilotCodexExecPolicyDiscoveryResult Empty { get; } = new(
            Array.Empty<CopilotCodexExecPolicyRule>(),
            Array.Empty<CopilotCodexExecPolicyIssue>(),
            Array.Empty<string>());
    }

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

    internal static partial class CopilotProjectInstructionDiscoveryConfig
    {
        internal const int MaximumConfiguredExecPolicyRules = 256;
        private const int MaximumConfiguredExecPolicyFilesPerLayer = 64;
        private const string RulesDirectoryName = "rules";
        private const string RulesFilePattern = "*.rules";

        private static CopilotCodexExecPolicyDiscoveryResult DiscoverExecPolicyForLayer(
            string allowedRootPath,
            string rulesDirectoryPath,
            CopilotProjectInstructionConfigSources source,
            int startingOrder)
        {
            var rules = new List<CopilotCodexExecPolicyRule>();
            var issues = new List<CopilotCodexExecPolicyIssue>();
            var sourceFilePaths = new List<string>();
            string normalizedDirectory;
            try
            {
                normalizedDirectory = Path.GetFullPath(rulesDirectoryPath);
                if (!Directory.Exists(normalizedDirectory)
                    || CopilotWorkspaceSearchSupport.HasReparsePointInPath(normalizedDirectory)
                    || !CopilotWorkspaceSearchSupport.IsPathWithinRoots(
                        normalizedDirectory,
                        [allowedRootPath]))
                {
                    return CopilotCodexExecPolicyDiscoveryResult.Empty;
                }
            }
            catch
            {
                return CopilotCodexExecPolicyDiscoveryResult.Empty;
            }

            string[] files;
            try
            {
                files = Directory.EnumerateFiles(
                        normalizedDirectory,
                        RulesFilePattern,
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .Take(MaximumConfiguredExecPolicyFilesPerLayer + 1)
                    .ToArray();
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException)
            {
                issues.Add(new CopilotCodexExecPolicyIssue(
                    normalizedDirectory,
                    "Codex exec-policy rules directory could not be enumerated."));
                return new CopilotCodexExecPolicyDiscoveryResult(
                    rules,
                    issues,
                    sourceFilePaths);
            }

            if (files.Length > MaximumConfiguredExecPolicyFilesPerLayer)
            {
                issues.Add(new CopilotCodexExecPolicyIssue(
                    normalizedDirectory,
                    $"Codex exec-policy discovery is limited to {MaximumConfiguredExecPolicyFilesPerLayer} rules files per active configuration layer."));
                files = files.Take(MaximumConfiguredExecPolicyFilesPerLayer).ToArray();
            }

            foreach (var filePath in files)
            {
                if (startingOrder + rules.Count >= MaximumConfiguredExecPolicyRules)
                {
                    issues.Add(new CopilotCodexExecPolicyIssue(
                        filePath,
                        $"Configured exec-policy rules are limited to {MaximumConfiguredExecPolicyRules} entries across active layers."));
                    break;
                }
                var normalizedPath = Path.GetFullPath(filePath);
                if (!TryReadConfigSource(allowedRootPath, normalizedPath, out var text))
                {
                    issues.Add(new CopilotCodexExecPolicyIssue(
                        normalizedPath,
                        "Codex exec-policy rules file was empty, oversized, unreadable, or outside its active configuration root."));
                    continue;
                }
                sourceFilePaths.Add(normalizedPath);
                var parsed = CopilotCodexExecPolicyParser.Parse(
                    normalizedPath,
                    source,
                    text,
                    startingOrder + rules.Count,
                    MaximumConfiguredExecPolicyRules - startingOrder - rules.Count);
                rules.AddRange(parsed.Rules);
                issues.AddRange(parsed.Issues);
            }

            return new CopilotCodexExecPolicyDiscoveryResult(
                rules.ToArray(),
                issues.ToArray(),
                sourceFilePaths.ToArray());
        }

        private static CopilotCodexExecPolicyDiscoveryResult DiscoverCodexHomeExecPolicy(
            string codexHomePath)
        {
            if (string.IsNullOrWhiteSpace(codexHomePath))
                return CopilotCodexExecPolicyDiscoveryResult.Empty;
            return DiscoverExecPolicyForLayer(
                codexHomePath,
                Path.Combine(codexHomePath, RulesDirectoryName),
                CopilotProjectInstructionConfigSources.CodexHome,
                startingOrder: 0);
        }

        private static CopilotCodexExecPolicyDiscoveryResult DiscoverTrustedProjectExecPolicy(
            string projectRootPath,
            IReadOnlyList<string> configDirectories,
            int startingOrder)
        {
            var rules = new List<CopilotCodexExecPolicyRule>();
            var issues = new List<CopilotCodexExecPolicyIssue>();
            var sourceFilePaths = new List<string>();
            foreach (var directoryPath in configDirectories)
            {
                var discovered = DiscoverExecPolicyForLayer(
                    projectRootPath,
                    Path.Combine(directoryPath, ".codex", RulesDirectoryName),
                    CopilotProjectInstructionConfigSources.TrustedProject,
                    startingOrder + rules.Count);
                rules.AddRange(discovered.Rules);
                issues.AddRange(discovered.Issues);
                sourceFilePaths.AddRange(discovered.SourceFilePaths);
                if (startingOrder + rules.Count >= MaximumConfiguredExecPolicyRules)
                    break;
            }
            return new CopilotCodexExecPolicyDiscoveryResult(
                rules.ToArray(),
                issues.ToArray(),
                sourceFilePaths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        }
    }

    internal static class CopilotCodexExecPolicyParser
    {
        public static CopilotCodexExecPolicyDiscoveryResult Parse(
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            string text,
            int startingOrder,
            int maximumRules)
        {
            var parser = new Parser(text ?? string.Empty);
            if (!parser.TryParseCalls(out var calls, out var syntaxError))
            {
                return new CopilotCodexExecPolicyDiscoveryResult(
                    Array.Empty<CopilotCodexExecPolicyRule>(),
                    [new CopilotCodexExecPolicyIssue(sourceFilePath, syntaxError)],
                    [sourceFilePath]);
            }

            var rules = new List<CopilotCodexExecPolicyRule>();
            var issues = new List<CopilotCodexExecPolicyIssue>();
            foreach (var call in calls)
            {
                if (!string.Equals(call.Name, "prefix_rule", StringComparison.Ordinal))
                {
                    issues.Add(new CopilotCodexExecPolicyIssue(
                        sourceFilePath,
                        $"Exec-policy declaration '{call.Name}' is not connected to ColorVision shell approvals."));
                    continue;
                }
                if (rules.Count >= Math.Max(0, maximumRules))
                {
                    issues.Add(new CopilotCodexExecPolicyIssue(
                        sourceFilePath,
                        $"Configured exec-policy rules are limited to {CopilotProjectInstructionDiscoveryConfig.MaximumConfiguredExecPolicyRules} entries across active layers."));
                    break;
                }
                if (!TryCreateRule(
                    sourceFilePath,
                    source,
                    call,
                    startingOrder + rules.Count,
                    out var rule,
                    out var error))
                {
                    issues.Add(new CopilotCodexExecPolicyIssue(sourceFilePath, error));
                    continue;
                }
                rules.Add(rule!);
            }

            return new CopilotCodexExecPolicyDiscoveryResult(
                rules.ToArray(),
                issues.ToArray(),
                [sourceFilePath]);
        }

        private static bool TryCreateRule(
            string sourceFilePath,
            CopilotProjectInstructionConfigSources source,
            ParsedCall call,
            int order,
            out CopilotCodexExecPolicyRule? rule,
            out string error)
        {
            rule = null;
            var allowedFields = new HashSet<string>(
                ["pattern", "decision", "justification", "match", "not_match"],
                StringComparer.Ordinal);
            var unknownField = call.Arguments.Keys.FirstOrDefault(key => !allowedFields.Contains(key));
            if (unknownField != null)
            {
                error = $"prefix_rule contains unsupported field '{unknownField}'.";
                return false;
            }
            if (!call.Arguments.TryGetValue("pattern", out var patternValue)
                || !TryReadPattern(patternValue, out var pattern))
            {
                error = "prefix_rule pattern must be a non-empty bounded list of string literals or literal unions.";
                return false;
            }
            var decision = CopilotCodexExecPolicyDecision.Allow;
            if (call.Arguments.TryGetValue("decision", out var decisionValue)
                && (!TryReadString(decisionValue, out var decisionText)
                    || !TryParseDecision(decisionText, out decision)))
            {
                error = "prefix_rule decision must be allow, prompt, or forbidden.";
                return false;
            }
            var justification = string.Empty;
            if (call.Arguments.TryGetValue("justification", out var justificationValue)
                && (!TryReadString(justificationValue, out justification)
                    || string.IsNullOrWhiteSpace(justification)
                    || justification.Length > 2_048
                    || justification.Any(char.IsControl)))
            {
                error = "prefix_rule justification must be a non-empty bounded single-line string.";
                return false;
            }

            var candidate = new CopilotCodexExecPolicyRule(
                sourceFilePath,
                source,
                pattern,
                decision,
                justification.Trim(),
                order);
            if (!candidate.IsStructurallyValid())
            {
                error = "prefix_rule is structurally invalid.";
                return false;
            }
            if (!TryValidateExamples(call.Arguments, candidate, out error))
                return false;
            rule = candidate;
            error = string.Empty;
            return true;
        }

        private static bool TryReadPattern(
            ParsedValue value,
            out IReadOnlyList<CopilotCodexExecPolicyPatternElement> pattern)
        {
            pattern = Array.Empty<CopilotCodexExecPolicyPatternElement>();
            if (value is not ParsedList list || list.Items.Count is < 1 or > 32)
                return false;
            var elements = new List<CopilotCodexExecPolicyPatternElement>(list.Items.Count);
            foreach (var item in list.Items)
            {
                IReadOnlyList<string> alternatives;
                if (item is ParsedString literal)
                {
                    alternatives = [literal.Value];
                }
                else if (item is ParsedList union
                    && union.Items.Count is > 0 and <= 16
                    && union.Items.All(candidate => candidate is ParsedString))
                {
                    alternatives = union.Items
                        .Cast<ParsedString>()
                        .Select(candidate => candidate.Value)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray();
                }
                else
                {
                    return false;
                }
                var element = new CopilotCodexExecPolicyPatternElement(alternatives);
                if (!element.IsStructurallyValid())
                    return false;
                elements.Add(element);
            }
            pattern = elements.ToArray();
            return true;
        }

        private static bool TryValidateExamples(
            IReadOnlyDictionary<string, ParsedValue> arguments,
            CopilotCodexExecPolicyRule rule,
            out string error)
        {
            foreach (var field in new[] { "match", "not_match" })
            {
                if (!arguments.TryGetValue(field, out var value))
                    continue;
                if (value is not ParsedList examples || examples.Items.Count > 64)
                {
                    error = $"prefix_rule {field} must be a bounded list of command examples.";
                    return false;
                }
                foreach (var example in examples.Items)
                {
                    if (!TryReadExample(example, out var command))
                    {
                        error = $"prefix_rule {field} contains an invalid command example.";
                        return false;
                    }
                    var matched = rule.Matches(command);
                    if (field == "match" && !matched)
                    {
                        error = $"prefix_rule match example `{string.Join(" ", command)}` does not match its pattern.";
                        return false;
                    }
                    if (field == "not_match" && matched)
                    {
                        error = $"prefix_rule not_match example `{string.Join(" ", command)}` unexpectedly matches its pattern.";
                        return false;
                    }
                }
            }
            error = string.Empty;
            return true;
        }

        private static bool TryReadExample(
            ParsedValue value,
            out IReadOnlyList<string> command)
        {
            command = Array.Empty<string>();
            if (value is ParsedString text)
                return TryTokenizeExample(text.Value, out command);
            if (value is not ParsedList list
                || list.Items.Count is < 1 or > 128
                || list.Items.Any(item => item is not ParsedString))
            {
                return false;
            }
            var tokens = list.Items.Cast<ParsedString>().Select(item => item.Value).ToArray();
            if (tokens.Any(token => string.IsNullOrWhiteSpace(token)
                || token.Length > 512
                || token.Any(char.IsControl)))
            {
                return false;
            }
            command = tokens;
            return true;
        }

        private static bool TryTokenizeExample(
            string text,
            out IReadOnlyList<string> command)
        {
            var tokens = new List<string>();
            var token = new StringBuilder();
            var quote = '\0';
            for (var index = 0; index < text.Length; index++)
            {
                var current = text[index];
                if (quote != '\0')
                {
                    if (current == quote)
                    {
                        quote = '\0';
                        continue;
                    }
                    if (current == '\\' && index + 1 < text.Length)
                    {
                        token.Append(text[++index]);
                        continue;
                    }
                    token.Append(current);
                    continue;
                }
                if (current is '\'' or '"')
                {
                    quote = current;
                    continue;
                }
                if (char.IsWhiteSpace(current))
                {
                    if (token.Length > 0)
                    {
                        tokens.Add(token.ToString());
                        token.Clear();
                    }
                    continue;
                }
                if (current == '\\' && index + 1 < text.Length)
                {
                    token.Append(text[++index]);
                    continue;
                }
                token.Append(current);
            }
            if (quote != '\0' || token.Length > 512)
            {
                command = Array.Empty<string>();
                return false;
            }
            if (token.Length > 0)
                tokens.Add(token.ToString());
            command = tokens.ToArray();
            return tokens.Count > 0
                && tokens.Count <= 128
                && tokens.All(item => item.Length <= 512 && !item.Any(char.IsControl));
        }

        private static bool TryReadString(ParsedValue value, out string text)
        {
            text = value is ParsedString literal ? literal.Value : string.Empty;
            return value is ParsedString;
        }

        private static bool TryParseDecision(
            string value,
            out CopilotCodexExecPolicyDecision decision)
        {
            decision = value switch
            {
                "allow" => CopilotCodexExecPolicyDecision.Allow,
                "prompt" => CopilotCodexExecPolicyDecision.Prompt,
                "forbidden" => CopilotCodexExecPolicyDecision.Forbidden,
                _ => CopilotCodexExecPolicyDecision.NoMatch,
            };
            return decision != CopilotCodexExecPolicyDecision.NoMatch;
        }

        private abstract record ParsedValue;

        private sealed record ParsedString(string Value) : ParsedValue;

        private sealed record ParsedList(IReadOnlyList<ParsedValue> Items) : ParsedValue;

        private sealed record ParsedCall(
            string Name,
            IReadOnlyDictionary<string, ParsedValue> Arguments);

        private sealed class Parser(string text)
        {
            private readonly string _text = text;
            private int _index;

            public bool TryParseCalls(
                out IReadOnlyList<ParsedCall> calls,
                out string error)
            {
                var parsed = new List<ParsedCall>();
                while (true)
                {
                    SkipTrivia();
                    if (_index >= _text.Length)
                    {
                        calls = parsed.ToArray();
                        error = string.Empty;
                        return true;
                    }
                    if (!TryReadIdentifier(out var name)
                        || !TryConsume('('))
                    {
                        calls = Array.Empty<ParsedCall>();
                        error = "Exec-policy rules file contains invalid Starlark call syntax.";
                        return false;
                    }
                    var arguments = new Dictionary<string, ParsedValue>(StringComparer.Ordinal);
                    SkipTrivia();
                    while (!TryConsume(')'))
                    {
                        if (!TryReadIdentifier(out var key)
                            || arguments.ContainsKey(key)
                            || !TryConsume('=')
                            || !TryReadValue(out var value))
                        {
                            calls = Array.Empty<ParsedCall>();
                            error = $"Exec-policy declaration '{name}' contains invalid named arguments.";
                            return false;
                        }
                        arguments.Add(key, value!);
                        SkipTrivia();
                        if (TryConsume(')'))
                            break;
                        if (!TryConsume(','))
                        {
                            calls = Array.Empty<ParsedCall>();
                            error = $"Exec-policy declaration '{name}' is missing a comma or closing parenthesis.";
                            return false;
                        }
                        SkipTrivia();
                        if (TryConsume(')'))
                            break;
                    }
                    parsed.Add(new ParsedCall(name, arguments));
                }
            }

            private bool TryReadValue(out ParsedValue? value)
            {
                SkipTrivia();
                if (_index >= _text.Length)
                {
                    value = null;
                    return false;
                }
                if (_text[_index] is '\'' or '"')
                {
                    if (!TryReadString(out var text))
                    {
                        value = null;
                        return false;
                    }
                    value = new ParsedString(text);
                    return true;
                }
                if (!TryConsume('['))
                {
                    value = null;
                    return false;
                }
                var items = new List<ParsedValue>();
                SkipTrivia();
                while (!TryConsume(']'))
                {
                    if (!TryReadValue(out var item))
                    {
                        value = null;
                        return false;
                    }
                    items.Add(item!);
                    SkipTrivia();
                    if (TryConsume(']'))
                        break;
                    if (!TryConsume(','))
                    {
                        value = null;
                        return false;
                    }
                    SkipTrivia();
                    if (TryConsume(']'))
                        break;
                }
                value = new ParsedList(items.ToArray());
                return true;
            }

            private bool TryReadIdentifier(out string value)
            {
                SkipTrivia();
                var start = _index;
                if (_index >= _text.Length
                    || !(_text[_index] == '_' || char.IsLetter(_text[_index])))
                {
                    value = string.Empty;
                    return false;
                }
                _index++;
                while (_index < _text.Length
                    && (_text[_index] == '_' || char.IsLetterOrDigit(_text[_index])))
                {
                    _index++;
                }
                value = _text[start.._index];
                return true;
            }

            private bool TryReadString(out string value)
            {
                var quote = _text[_index++];
                var builder = new StringBuilder();
                while (_index < _text.Length)
                {
                    var current = _text[_index++];
                    if (current == quote)
                    {
                        value = builder.ToString();
                        return true;
                    }
                    if (current != '\\')
                    {
                        builder.Append(current);
                        continue;
                    }
                    if (_index >= _text.Length)
                        break;
                    var escaped = _text[_index++];
                    builder.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '\'' => '\'',
                        '"' => '"',
                        _ => '\0',
                    });
                    if (builder[^1] == '\0')
                    {
                        value = string.Empty;
                        return false;
                    }
                }
                value = string.Empty;
                return false;
            }

            private bool TryConsume(char expected)
            {
                SkipTrivia();
                if (_index >= _text.Length || _text[_index] != expected)
                    return false;
                _index++;
                return true;
            }

            private void SkipTrivia()
            {
                while (_index < _text.Length)
                {
                    if (char.IsWhiteSpace(_text[_index]))
                    {
                        _index++;
                        continue;
                    }
                    if (_text[_index] != '#')
                        break;
                    while (_index < _text.Length && _text[_index] is not '\r' and not '\n')
                        _index++;
                }
            }
        }
    }
}
