using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;

namespace ColorVision.Copilot
{
    internal partial class CopilotCodeReviewWindow : Window
    {
        private static readonly string[] ThemeResourceKeys =
        {
            "GlobalBackground",
            "GlobalBorderBrush",
            "GlobalBorderBrush1",
            "GlobalTextBrush",
            "SecondaryTextBrush",
            "ButtonBackground",
            "ButtonBorderBrush",
            "PrimaryBrush",
        };

        private readonly CopilotCodeReviewWindowModel _model;

        internal CopilotCodeReviewWindow(CopilotChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            if (message.IsUser
                || message.RequestMode != CopilotAgentMode.Review
                || message.CodeReviewSnapshot?.IsStructurallyValid() != true)
            {
                throw new ArgumentException("A completed Review message with a valid snapshot is required.", nameof(message));
            }

            InitializeComponent();
            this.ApplyCaption();
            _model = CopilotCodeReviewWindowModel.Create(message);
            DataContext = _model;
            Loaded += CopilotCodeReviewWindow_Loaded;
        }

        private void CopilotCodeReviewWindow_Loaded(object sender, RoutedEventArgs e) =>
            ApplyOwnerThemeResources();

        private void ApplyOwnerThemeResources()
        {
            if (Owner == null)
                return;

            foreach (var key in ThemeResourceKeys)
            {
                var value = Owner.TryFindResource(key);
                if (value != null)
                    Resources[key] = value;
            }
        }

        private void CopyConclusionButton_Click(object sender, RoutedEventArgs e) =>
            CopyText(_model.ConclusionText, "审查结论");

        private void CopyFindingsButton_Click(object sender, RoutedEventArgs e) =>
            CopyText(_model.FindingsText, "行级 Findings");

        private void CopyDiffButton_Click(object sender, RoutedEventArgs e) =>
            CopyText(_model.DiffText, "Diff");

        private void CopyModelObservationButton_Click(object sender, RoutedEventArgs e) =>
            CopyText(_model.ModelObservationText, "模型证据原文");

        private void CopyText(string text, string label)
        {
            try
            {
                Clipboard.SetText(text ?? string.Empty);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    this,
                    $"无法复制{label}：{CopilotUserFacingErrorFormatter.Sanitize(ex.Message)}",
                    "ColorVision",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
    }

    internal sealed class CopilotCodeReviewWindowModel
    {
        public string ConclusionText { get; init; } = string.Empty;

        public IReadOnlyList<CopilotCodeReviewFindingViewModel> Findings { get; init; } =
            Array.Empty<CopilotCodeReviewFindingViewModel>();

        public string FindingsText { get; init; } = string.Empty;

        public string FindingsTabHeader { get; init; } = "行级 Findings";

        public bool HasFindings { get; init; }

        public bool ShowFindingsStatus => !HasFindings;

        public string FindingsStatusText { get; init; } = string.Empty;

        public string DiffText { get; init; } = string.Empty;

        public string ModelObservationText { get; init; } = string.Empty;

        public string RepositoryRoot { get; init; } = string.Empty;

        public string TargetLabel { get; init; } = string.Empty;

        public string ScopeLabel { get; init; } = string.Empty;

        public string EvidenceLabel { get; init; } = string.Empty;

        public string PathLabel { get; init; } = string.Empty;

        public bool HasEvidenceWarning { get; init; }

        public string EvidenceWarning { get; init; } = string.Empty;

        public static CopilotCodeReviewWindowModel Create(CopilotChatMessage message)
        {
            ArgumentNullException.ThrowIfNull(message);
            var snapshot = message.CodeReviewSnapshot
                ?? throw new ArgumentException("Code review snapshot is required.", nameof(message));
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Code review snapshot is invalid.", nameof(message));

            snapshot.TryReadModelObservation(out _, out var modelObservationTruncated);
            var hasStructuredModelDiff = snapshot.TryReadStructuredModelDiff(out _);
            var hasFindingsSubmission = snapshot.TryReadFindings(out var findings);
            var findingModels = findings
                .Select(CopilotCodeReviewFindingViewModel.Create)
                .ToArray();
            return new CopilotCodeReviewWindowModel
            {
                ConclusionText = message.Content ?? string.Empty,
                Findings = findingModels,
                FindingsText = BuildFindingsText(hasFindingsSubmission, findings),
                FindingsTabHeader = hasFindingsSubmission
                    ? $"行级 Findings ({findings.Count})"
                    : "行级 Findings (待提交)",
                HasFindings = findingModels.Length > 0,
                FindingsStatusText = hasFindingsSubmission
                    ? "已提交结构化结果：未发现可定位到模型可见 Diff hunk 的可操作问题。"
                    : "本次 Review 尚未提交与最新 Diff 绑定的结构化 findings。通常表示运行被中断、失败，或来自旧版本会话。",
                DiffText = BuildDiffText(snapshot),
                ModelObservationText = snapshot.ModelObservation,
                RepositoryRoot = snapshot.RepositoryRoot,
                TargetLabel = FormatTarget(snapshot),
                ScopeLabel = FormatScope(snapshot),
                EvidenceLabel = (snapshot.HasChanges ? "包含变更" : "未发现变更")
                    + (snapshot.ToolOutputComplete ? " · 工具输出完整" : " · 工具输出有界")
                    + (modelObservationTruncated
                        ? " · 模型证据已裁剪"
                        : hasStructuredModelDiff
                            ? " · 模型 Diff 完整"
                            : " · 模型结果已替换"),
                PathLabel = string.IsNullOrWhiteSpace(snapshot.PathFilter)
                    ? "整个仓库"
                    : snapshot.PathFilter,
                HasEvidenceWarning = snapshot.ToolPatchTruncated
                    || modelObservationTruncated
                    || !hasStructuredModelDiff,
                EvidenceWarning = BuildEvidenceWarning(
                    snapshot,
                    modelObservationTruncated,
                    hasStructuredModelDiff),
            };
        }

        private static string BuildFindingsText(
            bool hasSubmission,
            IReadOnlyList<CopilotCodeReviewFinding> findings)
        {
            if (!hasSubmission)
                return "结构化 findings 尚未提交。";
            if (findings.Count == 0)
                return "未发现可操作的行级 finding。";

            var builder = new StringBuilder();
            foreach (var finding in findings)
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();
                builder.Append('[').Append(finding.Priority).Append("] ")
                    .AppendLine(finding.Title)
                    .Append(finding.Path).Append(':').Append(finding.LineStart);
                if (finding.LineEnd != finding.LineStart)
                    builder.Append('-').Append(finding.LineEnd);
                builder.Append(" · ").AppendLine(finding.Side)
                    .Append(finding.Body);
            }
            return builder.ToString();
        }

        internal static string BuildDiffText(CopilotCodeReviewSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            if (!snapshot.IsStructurallyValid())
                throw new ArgumentException("Code review snapshot is invalid.", nameof(snapshot));

            if (!snapshot.TryReadStructuredModelDiff(out var modelDiff))
            {
                snapshot.TryReadModelObservation(out var modelContent, out _);
                return modelContent;
            }

            var builder = new StringBuilder();
            foreach (var section in modelDiff.Sections)
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();
                builder.Append("===== ").Append(FormatSection(section.Scope)).AppendLine(" =====");
                builder.Append(string.IsNullOrWhiteSpace(section.Patch)
                    ? "（所选范围没有补丁）"
                    : section.Patch.TrimEnd());
            }
            return builder.ToString();
        }

        private static string BuildEvidenceWarning(
            CopilotCodeReviewSnapshot snapshot,
            bool modelObservationTruncated,
            bool hasStructuredModelDiff)
        {
            if (hasStructuredModelDiff
                && !snapshot.ToolPatchTruncated
                && !modelObservationTruncated)
            {
                return string.Empty;
            }
            if (!hasStructuredModelDiff && !modelObservationTruncated)
            {
                return "PostToolUse hook 替换了模型可见的 Git Diff，或返回格式已不再可解析。下方按原样保留 Agent 实际收到的内容；不能把原始工具补丁视为已审查。";
            }
            if (snapshot.ToolPatchTruncated && modelObservationTruncated)
            {
                return "Git 工具先生成了有界补丁，模型输出预算又裁剪了该结果。结论只能覆盖下方保存的模型可见证据，不能把两层省略内容视为已审查。";
            }
            if (snapshot.ToolPatchTruncated)
            {
                return "Git 工具结果达到本地补丁上限。结论只能覆盖下方保存的有界 Diff，不能把工具省略部分视为已审查。";
            }
            return "模型输出预算裁剪了 Git 工具结果。下方保留的是 Agent 实际收到的有界内容，不能把模型未收到的部分视为已审查。";
        }

        private static string FormatTarget(CopilotCodeReviewSnapshot snapshot) => snapshot.Target switch
        {
            "base_branch" => $"HEAD 相对基线 {snapshot.Revision}",
            "commit" => $"提交 {snapshot.Revision}",
            _ => "当前未提交变更",
        };

        private static string FormatScope(CopilotCodeReviewSnapshot snapshot)
        {
            if (!string.Equals(snapshot.Target, "working_tree", StringComparison.Ordinal))
                return "固定修订补丁 · " + snapshot.ResolvedRevision;

            return snapshot.Scope switch
            {
                "both" => "已暂存 + 未暂存 + 未跟踪",
                "staged" => "已暂存",
                _ => "未暂存 + 未跟踪",
            };
        }

        private static string FormatSection(string scope) => scope switch
        {
            "staged" => "已暂存 Diff",
            "unstaged" => "未暂存 Diff",
            "untracked" => "未跟踪文件内容",
            "base_branch" => "基线比较 Diff",
            "commit" => "提交 Diff",
            _ => scope,
        };
    }

    internal sealed class CopilotCodeReviewFindingViewModel
    {
        public string Priority { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string Body { get; init; } = string.Empty;

        public string LocationLabel { get; init; } = string.Empty;

        public static CopilotCodeReviewFindingViewModel Create(CopilotCodeReviewFinding finding)
        {
            ArgumentNullException.ThrowIfNull(finding);
            var line = finding.LineEnd == finding.LineStart
                ? finding.LineStart.ToString()
                : $"{finding.LineStart}-{finding.LineEnd}";
            return new CopilotCodeReviewFindingViewModel
            {
                Priority = finding.Priority,
                Title = finding.Title,
                Body = finding.Body,
                LocationLabel = $"{finding.Path}:{line} · {(finding.Side == "old" ? "旧行" : "新行")}",
            };
        }
    }
}
