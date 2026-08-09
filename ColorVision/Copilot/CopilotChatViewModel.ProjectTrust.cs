using System;
using System.Windows;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotProjectTrustAdmissionDecision(
        bool IsAllowed,
        bool TrustPersisted);

    internal static class CopilotProjectTrustSubmissionAdmission
    {
        public static bool TryResolve(
            CopilotAgentHostContextSnapshot initialSnapshot,
            Func<CopilotAgentHostContextSnapshot, CopilotProjectTrustAdmissionDecision> evaluate,
            Func<CopilotAgentHostContextSnapshot> recapture,
            out CopilotAgentHostContextSnapshot resolvedSnapshot)
        {
            ArgumentNullException.ThrowIfNull(initialSnapshot);
            ArgumentNullException.ThrowIfNull(evaluate);
            ArgumentNullException.ThrowIfNull(recapture);
            resolvedSnapshot = initialSnapshot;
            var decision = evaluate(initialSnapshot);
            if (!decision.IsAllowed)
                return false;

            if (decision.TrustPersisted)
                resolvedSnapshot = recapture();
            return true;
        }
    }

    public partial class CopilotChatViewModel
    {
        private bool TryResolveProjectTrustForSubmission(
            CopilotAgentHostContextSnapshot initialSnapshot,
            Func<CopilotAgentHostContextSnapshot> recapture,
            out CopilotAgentHostContextSnapshot resolvedSnapshot) =>
            CopilotProjectTrustSubmissionAdmission.TryResolve(
                initialSnapshot,
                snapshot =>
                {
                    var isAllowed = TryConfirmProjectDirectoryTrust(snapshot, out var trustPersisted);
                    return new CopilotProjectTrustAdmissionDecision(isAllowed, trustPersisted);
                },
                recapture,
                out resolvedSnapshot);

        private bool TryConfirmProjectDirectoryTrust(
            CopilotAgentHostContextSnapshot turnSnapshot,
            out bool trustPersisted)
        {
            trustPersisted = false;
            if (!CopilotCodexProjectTrustPersistence.RequiresDecision(
                turnSnapshot.PrimaryTrustedProjectRootPath,
                turnSnapshot.ProjectInstructionDiscoveryOptions))
            {
                return true;
            }

            var trustTarget = turnSnapshot.PrimaryTrustedProjectRootPath;
            var currentDirectory = string.IsNullOrWhiteSpace(turnSnapshot.ProjectConfigWorkingDirectoryPath)
                ? trustTarget
                : turnSnapshot.ProjectConfigWorkingDirectoryPath;
            var location = string.Equals(currentDirectory, trustTarget, StringComparison.OrdinalIgnoreCase)
                ? trustTarget
                : currentDirectory + Environment.NewLine
                    + Environment.NewLine
                    + "当前目录位于项目内；信任将应用到项目根目录：" + Environment.NewLine
                    + trustTarget;
            var decision = MessageBox.Show(
                Application.Current.GetActiveWindow(),
                "是否信任此目录中的内容？" + Environment.NewLine
                + Environment.NewLine
                + location + Environment.NewLine
                + Environment.NewLine
                + "信任后会加载项目本地 .codex/config.toml、自定义子代理以及其中的执行与审批策略。"
                + "不受信任的项目内容可能带来提示注入风险。",
                "ColorVision · 信任项目目录",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (decision != MessageBoxResult.Yes)
            {
                SetPendingActionFeedback("已取消本次发送；草稿和项目信任配置均未改变。");
                return false;
            }

            if (!CopilotCodexProjectTrustPersistence.TryTrustProject(
                turnSnapshot.GlobalInstructionRootPath,
                trustTarget,
                out var error))
            {
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    "项目信任保存失败；本次发送已取消。" + Environment.NewLine
                    + error,
                    "ColorVision · 项目信任未保存",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return false;
            }

            trustPersisted = true;
            SetPendingActionFeedback("已信任项目根目录；本轮将重新加载项目 .codex/config.toml 后执行。");
            return true;
        }
    }
}
