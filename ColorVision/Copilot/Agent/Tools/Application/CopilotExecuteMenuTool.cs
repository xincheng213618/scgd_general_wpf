using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public sealed class CopilotExecuteMenuTool : ICopilotTool
    {
        public string Name => "ExecuteMenu";

        public string Description => "按菜单名称或菜单路径执行主菜单命令，例如 选项、VAM、检查更新、深色主题、英文。input.query 直接填写目标菜单即可。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || Application.Current == null)
                return false;

            if (!CopilotMenuToolSupport.HasMenuIntent(request.UserText))
                return false;

            return Application.Current.Dispatcher.Invoke(() =>
            {
                var result = CopilotMenuToolSupport.Resolve(request.UserText);
                return result.Candidates.Count > 0;
            });
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (Application.Current == null)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法执行菜单。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var sourceText = string.IsNullOrWhiteSpace(toolInput?.Query)
                ? request.UserText
                : toolInput.Query;

            var matchResult = await Application.Current.Dispatcher.InvokeAsync(() => CopilotMenuToolSupport.Resolve(sourceText));
            if (matchResult.BestCandidate == null || !matchResult.HasStrongMatch)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "没有找到可执行的菜单匹配项。",
                    Content = BuildCandidateList(matchResult.Suggestions),
                    ErrorMessage = "请把菜单名说得更具体，例如“打开选项”、“打开 VAM”或“检查更新”。",
                };
            }

            if (matchResult.IsAmbiguous)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = $"匹配到多个候选菜单，暂不自动执行“{matchResult.BestCandidate.DisplayHeader}”。",
                    Content = BuildCandidateList(matchResult.Candidates),
                    ErrorMessage = "请补充更具体的菜单名或完整路径。",
                };
            }

            var selectedCandidate = matchResult.BestCandidate;
            if (!selectedCandidate.CanExecute || selectedCandidate.MenuItem.Command?.CanExecute(null) != true)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = $"菜单“{selectedCandidate.DisplayPath}”当前不可执行。",
                    Content = BuildCandidateList(matchResult.Candidates),
                    ErrorMessage = "该菜单可能受权限、当前状态或上下文限制。",
                };
            }

            _ = Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (selectedCandidate.MenuItem.Command?.CanExecute(null) == true)
                        selectedCandidate.MenuItem.Command.Execute(null);
                }
                catch
                {
                }
            }));

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"已调度执行菜单“{selectedCandidate.DisplayPath}”。",
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[目标菜单] {selectedCandidate.DisplayPath}",
                    $"[执行状态] 已通过 UI 线程调度执行",
                }),
            };
        }

        private static string BuildCandidateList(System.Collections.Generic.IReadOnlyList<CopilotMenuToolSupport.MenuMatchCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "[候选菜单] 无";

            var lines = candidates
                .Take(5)
                .Select((candidate, index) => $"{index + 1}. {candidate.DisplayPath}{(candidate.CanExecute ? string.Empty : " [当前不可执行]")}")
                .ToArray();

            return string.Join(Environment.NewLine, new[]
            {
                "[候选菜单]",
                string.Join(Environment.NewLine, lines),
            });
        }
    }
}