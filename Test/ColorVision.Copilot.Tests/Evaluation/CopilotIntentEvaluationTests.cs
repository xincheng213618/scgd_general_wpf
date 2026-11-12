using System.IO;
using System.Text;
using Xunit.Abstractions;

namespace ColorVision.Copilot.Tests.Evaluation;

// Measures the keyword intent policy against labeled prompts through the real tool registry and
// execution contract. The score is a report, not a gate: mismatches describe the current policy.
public sealed class CopilotIntentEvaluationTests(ITestOutputHelper output)
{
    private const string ReportPathVariable = "COPILOT_INTENT_REPORT";

    private static readonly IReadOnlyDictionary<CopilotIntentTool, string[]> ToolNames = new Dictionary<CopilotIntentTool, string[]>
    {
        [CopilotIntentTool.LocalSearch] = [CopilotSharedAgentToolNames.GrepText, CopilotSharedAgentToolNames.SearchFiles],
        [CopilotIntentTool.WorkspaceWrite] = ["ApplyWorkspacePatchEnvelope"],
        [CopilotIntentTool.Execute] = ["RunShellCommand", "StartBackgroundShellCommand", "RunWorkspaceValidation"],
        [CopilotIntentTool.Shell] = ["RunShellCommand", "StartBackgroundShellCommand"],
        [CopilotIntentTool.GitDiff] = ["InspectGitDiff"],
        [CopilotIntentTool.GitStatus] = ["InspectGitWorkingTree"],
        [CopilotIntentTool.DatabaseRead] = ["QueryDatabaseSql"],
        [CopilotIntentTool.DatabaseWrite] = ["ExecuteDatabaseSql"],
        [CopilotIntentTool.FlowInspect] = [CopilotSharedAgentToolNames.InspectFlowGraph],
        [CopilotIntentTool.FlowWrite] = [CopilotSharedAgentToolNames.ApplyFlowPatch],
        [CopilotIntentTool.FlowRunHistory] = ["QueryFlowExecutionStats", CopilotSharedAgentToolNames.ListFlowRuns],
        [CopilotIntentTool.DeviceServices] = [CopilotSharedAgentToolNames.InspectDeviceServices],
        [CopilotIntentTool.RecentLog] = [CopilotSharedAgentToolNames.GetRecentLog],
        [CopilotIntentTool.WebSearch] = ["WebSearch"],
        [CopilotIntentTool.FetchUrl] = ["FetchUrl"],
        [CopilotIntentTool.WindowsProcess] = ["InspectWindowsProcesses"],
        [CopilotIntentTool.WindowsService] = ["InspectWindowsServices"],
        [CopilotIntentTool.TcpPort] = ["InspectTcpPort"],
    };

    [Fact]
    public void SamplesAndToolMappingStayEvaluable()
    {
        var samples = CopilotIntentSamples.All;
        Assert.Empty(samples.GroupBy(sample => sample.Id).Where(group => group.Count() > 1).Select(group => group.Key));
        Assert.Empty(samples.GroupBy(sample => (sample.Prompt, sample.Context)).Where(group => group.Count() > 1).Select(group => group.Key));
        Assert.Empty(samples.Where(sample => sample.Needs.Intersect(sample.Forbids).Any()).Select(sample => sample.Id));

        // A renamed tool must fail here rather than quietly turn into a "missing tool" mismatch.
        var registered = CopilotToolRegistry.CreateDefaultTools().Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in Enum.GetValues<CopilotIntentTool>())
            Assert.All(ToolNames[tool], name => Assert.Contains(name, registered));
    }

    [Fact]
    public void ReportsCurrentPolicyAgainstSamples()
    {
        var root = Path.GetTempPath();
        var registry = new CopilotToolRegistry(CopilotToolRegistry.CreateDefaultTools());
        var results = CopilotIntentSamples.All.Select(sample => Evaluate(sample, registry, root)).ToArray();

        var report = BuildReport(results);
        var reportPath = Environment.GetEnvironmentVariable(ReportPathVariable);
        if (string.IsNullOrWhiteSpace(reportPath))
            reportPath = Path.Combine(AppContext.BaseDirectory, "copilot-intent-report.md");
        File.WriteAllText(reportPath, report, new UTF8Encoding(false));
        output.WriteLine(report);
    }

    private static IntentResult Evaluate(CopilotIntentSample sample, CopilotToolRegistry registry, string root)
    {
        var request = new CopilotAgentRequest
        {
            Mode = CopilotAgentMode.Auto,
            UserText = sample.Prompt,
            WritableLocalRootPaths = sample.Context == CopilotIntentContext.NoWorkspace ? [] : [root],
            SearchRootPaths = sample.Context == CopilotIntentContext.NoWorkspace ? [] : [root],
            ContextItems = sample.Context == CopilotIntentContext.Flow
                ? [new CopilotContextItem { Title = "Flow context", Content = "synthetic" }]
                : [],
        };
        var tools = registry.FindTools(request);
        var visible = tools.Select(tool => tool.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var contract = CopilotAgentExecutionContract.Create(request, tools);
        var enforced = contract.IsRequired ? ToEvidence(contract.Requirement) : CopilotIntentEvidence.None;
        var actual = enforced | ToEvidence(contract.IntentHintRequirement);
        return new IntentResult(
            sample,
            sample.Needs.Where(tool => !ToolNames[tool].Any(visible.Contains)).ToArray(),
            sample.Forbids.Where(tool => ToolNames[tool].Any(visible.Contains)).ToArray(),
            sample.Evidence & ~actual,
            actual & ~sample.Evidence,
            enforced & ~sample.Evidence,
            visible.Count,
            CopilotAgentToolSurfaceMetrics.Capture(tools.Count, tools, null).AvailableToolDefinitionCharacters);
    }

    private static CopilotIntentEvidence ToEvidence(CopilotAgentExecutionRequirement requirement)
    {
        var name = requirement.ToString();
        var evidence = CopilotIntentEvidence.None;
        if (name.Contains("WorkspaceEdit", StringComparison.Ordinal))
            evidence |= CopilotIntentEvidence.Edit;
        if (name.Contains("WorkspaceCreate", StringComparison.Ordinal))
            evidence |= CopilotIntentEvidence.Create;
        if (name.Contains("WorkspaceRollback", StringComparison.Ordinal))
            evidence |= CopilotIntentEvidence.Rollback;
        if (name.Contains("ShellExecution", StringComparison.Ordinal) || name.Contains("Validation", StringComparison.Ordinal))
            evidence |= CopilotIntentEvidence.Execute;
        if (requirement is CopilotAgentExecutionRequirement.DirectUrlEvidence or CopilotAgentExecutionRequirement.PublicWebSearch)
            evidence |= CopilotIntentEvidence.Web;
        if (name.Contains("BatchImage", StringComparison.Ordinal))
            evidence |= CopilotIntentEvidence.BatchImage;
        return evidence;
    }

    private static string BuildReport(IReadOnlyList<IntentResult> results)
    {
        var total = results.Count;
        var builder = new StringBuilder();
        builder.AppendLine("# Copilot intent policy report");
        builder.AppendLine();
        builder.AppendLine("Synthetic labeled prompts; not a production error rate.");
        builder.AppendLine();
        builder.AppendLine($"- samples: {total}");
        builder.AppendLine($"- fully matched: {results.Count(result => result.Matched)} ({Percent(results.Count(result => result.Matched), total)})");
        AppendRate(builder, "needed tool hidden", results.Count(result => result.Needs.Length > 0), results.Count(result => result.MissingTools.Length > 0));
        AppendRate(builder, "forbidden tool visible", results.Count(result => result.Forbids.Length > 0), results.Count(result => result.ForbiddenVisible.Length > 0));
        AppendRate(builder, "intended action neither enforced nor hinted", results.Count(result => result.Expected != CopilotIntentEvidence.None), results.Count(result => result.MissedEvidence != CopilotIntentEvidence.None));
        AppendRate(builder, "action wrongly enforced or hinted", total, results.Count(result => result.ExtraEvidence != CopilotIntentEvidence.None));
        AppendRate(builder, "action wrongly enforced (can block the turn)", total, results.Count(result => result.WronglyEnforced != CopilotIntentEvidence.None));
        builder.AppendLine($"- average visible tools: {results.Average(result => result.VisibleToolCount):F1}");
        builder.AppendLine($"- average tool definition characters: {results.Average(result => result.ToolDefinitionCharacters):F0}");
        builder.AppendLine();
        builder.AppendLine("| category | samples | matched |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var group in results.GroupBy(result => result.Sample.Category))
            builder.AppendLine($"| {group.Key} | {group.Count()} | {group.Count(result => result.Matched)} |");
        builder.AppendLine();
        builder.AppendLine("| id | prompt | mismatch |");
        builder.AppendLine("| --- | --- | --- |");
        foreach (var result in results.Where(result => !result.Matched))
            builder.AppendLine($"| {result.Sample.Id} | {result.Sample.Prompt} | {result.Describe()} |");
        return builder.ToString();
    }

    private static void AppendRate(StringBuilder builder, string label, int applicable, int misses) =>
        builder.AppendLine($"- {label}: {misses}/{applicable} ({Percent(misses, applicable)})");

    private static string Percent(int part, int whole) => whole == 0 ? "n/a" : $"{100.0 * part / whole:F1}%";

    private sealed record IntentResult(
        CopilotIntentSample Sample,
        CopilotIntentTool[] MissingTools,
        CopilotIntentTool[] ForbiddenVisible,
        CopilotIntentEvidence MissedEvidence,
        CopilotIntentEvidence ExtraEvidence,
        CopilotIntentEvidence WronglyEnforced,
        int VisibleToolCount,
        int ToolDefinitionCharacters)
    {
        public CopilotIntentTool[] Needs => Sample.Needs;

        public CopilotIntentTool[] Forbids => Sample.Forbids;

        public CopilotIntentEvidence Expected => Sample.Evidence;

        public bool Matched => MissingTools.Length == 0
            && ForbiddenVisible.Length == 0
            && MissedEvidence == CopilotIntentEvidence.None
            && ExtraEvidence == CopilotIntentEvidence.None;

        public string Describe()
        {
            var parts = new List<string>();
            if (MissingTools.Length > 0)
                parts.Add($"hidden: {string.Join(", ", MissingTools)}");
            if (ForbiddenVisible.Length > 0)
                parts.Add($"visible: {string.Join(", ", ForbiddenVisible)}");
            if (MissedEvidence != CopilotIntentEvidence.None)
                parts.Add($"no signal: {MissedEvidence}");
            if (ExtraEvidence != CopilotIntentEvidence.None)
                parts.Add($"{(WronglyEnforced != CopilotIntentEvidence.None ? "wrongly enforced" : "wrongly hinted")}: {ExtraEvidence}");
            return string.Join("; ", parts);
        }
    }
}
