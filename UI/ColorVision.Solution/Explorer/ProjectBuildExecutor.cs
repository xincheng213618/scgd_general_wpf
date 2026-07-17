using ColorVision.Solution.Terminal;

namespace ColorVision.Solution.Explorer
{
    public static class ProjectBuildExecutor
    {
        public static bool TryCreateCommandBatch(
            ProjectBuildPlan plan,
            out IReadOnlyList<TerminalCommandRequest> commands,
            out string errorMessage)
        {
            if (!plan.IsValid)
            {
                commands = Array.Empty<TerminalCommandRequest>();
                errorMessage = plan.FormatDiagnostics();
                return false;
            }

            var result = new List<TerminalCommandRequest>();
            var preparationErrors = new List<string>();
            foreach (ProjectDefinition project in plan.OrderedProjects)
            {
                string configurationSuffix = string.IsNullOrWhiteSpace(project.ActiveConfiguration)
                    ? string.Empty
                    : $" ({project.ActiveConfiguration})";
                if (!ProjectProviderRegistry.HasCapability(project, ProjectCapabilityIds.Build))
                    continue;

                if (!ProjectProviderRegistry.TryCreateCapabilityInvocation(
                    project,
                    ProjectCapabilityIds.Build,
                    out ProjectCommandInvocation? invocation)
                    || invocation == null)
                {
                    preparationErrors.Add($"无法准备项目“{project.Name}{configurationSuffix}”的生成命令，请检查工作目录和项目配置。");
                    continue;
                }

                result.Add(new TerminalCommandRequest(
                    $"生成 {project.Name}{configurationSuffix}",
                    invocation.Command,
                    invocation.WorkingDirectory));
            }

            if (preparationErrors.Count > 0)
            {
                commands = Array.Empty<TerminalCommandRequest>();
                errorMessage = string.Join(Environment.NewLine, preparationErrors.Select(error => $"• {error}"));
                return false;
            }
            if (result.Count == 0)
            {
                commands = Array.Empty<TerminalCommandRequest>();
                errorMessage = "当前构建范围内没有配置生成命令的项目。";
                return false;
            }

            commands = result;
            errorMessage = string.Empty;
            return true;
        }

        public static bool Execute(ProjectBuildPlan plan, out string errorMessage)
        {
            if (!TryCreateCommandBatch(plan, out IReadOnlyList<TerminalCommandRequest> commands, out errorMessage))
                return false;

            if (TerminalService.GetInstance().TrySendCommandBatch(commands))
                return true;

            errorMessage = "终端面板尚未初始化，无法提交生成命令。";
            return false;
        }
    }
}
