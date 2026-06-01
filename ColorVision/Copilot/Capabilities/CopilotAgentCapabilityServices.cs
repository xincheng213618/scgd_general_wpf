using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Languages;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot
{
    public static class CopilotDocsCapability
    {
        public static bool HasDocumentationIntent(string text) => CopilotDocsToolSupport.HasDocumentationIntent(text);

        public static string ResolveQuery(string? userText, string? toolQuery)
        {
            var query = (toolQuery ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(query) ? (userText ?? string.Empty).Trim() : query;
        }

        public static async Task<CopilotCapabilityResult> SearchAsync(string? query, CancellationToken cancellationToken)
        {
            var resolvedQuery = (query ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(resolvedQuery))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "缺少文档检索问题或关键字。",
                    ErrorMessage = "请提供更具体的软件问题、页面名称或功能关键字。",
                };
            }

            try
            {
                var searchResult = await CopilotDocsToolSupport.SearchAsync(resolvedQuery, cancellationToken);
                if (searchResult.Hits.Count == 0)
                {
                    return new CopilotCapabilityResult
                    {
                        Success = false,
                        Summary = "在线文档中没有检索到相关片段。",
                        ErrorMessage = "请把问题说得更具体，例如功能名、页面名、菜单名、设备名或模块名。",
                    };
                }

                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"已从在线文档命中 {searchResult.DistinctPageCount} 个页面，返回 {searchResult.Hits.Count} 个相关片段。",
                    Content = CopilotDocsToolSupport.BuildContextBlock(searchResult),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "无法读取在线文档索引。",
                    ErrorMessage = ex.Message,
                };
            }
        }
    }

    public static class CopilotRecentLogCapability
    {
        public static bool HasAvailableLogFile() => CopilotRecentLogSupport.HasAvailableLogFile();

        public static CopilotCapabilityResult Capture(string? query, CopilotRecentLogMode mode, int maxLines, int maxChars)
        {
            var snapshot = CopilotRecentLogSupport.Capture(query, mode, maxLines, maxChars);
            if (!snapshot.Success)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = snapshot.Summary,
                    ErrorMessage = snapshot.ErrorMessage,
                };
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = snapshot.Summary,
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[日志文件] {snapshot.FilePath}",
                    snapshot.Content,
                }),
            };
        }
    }

    public static class CopilotApplicationCapability
    {
        public static bool HasMenuIntent(string? text) => CopilotMenuToolSupport.HasMenuIntent(text);

        public static bool HasThemeIntent(string? text) => CopilotApplicationControlSupport.HasThemeIntent(text);

        public static bool HasLanguageIntent(string? text) => CopilotApplicationControlSupport.HasLanguageIntent(text);

        public static bool HasMenuCandidates(string? text)
        {
            if (Application.Current == null)
                return false;

            return Application.Current.Dispatcher.Invoke(() => CopilotMenuToolSupport.Resolve(text).Candidates.Count > 0);
        }

        public static async Task<CopilotCapabilityResult> ExecuteMenuAsync(string? sourceText, CancellationToken cancellationToken)
        {
            return await ExecuteMenuAsync(sourceText, dryRun: false, allowConfirmationRequired: true, cancellationToken);
        }

        public static async Task<CopilotCapabilityResult> ExecuteMenuAsync(string? sourceText, bool dryRun, bool allowConfirmationRequired, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Application.Current == null)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法执行菜单。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var matchResult = await Application.Current.Dispatcher.InvokeAsync(() => CopilotMenuToolSupport.Resolve(sourceText));
            if (matchResult.BestCandidate == null || !matchResult.HasStrongMatch)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "没有找到可执行的菜单匹配项。",
                    Content = BuildMenuOperationContent(null, matchResult.Suggestions, dryRun, "not_found"),
                    ErrorMessage = "请把菜单名说得更具体，例如“打开选项”、“打开 VAM”或“检查更新”。",
                };
            }

            if (matchResult.IsAmbiguous)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"匹配到多个候选菜单，暂不自动执行“{matchResult.BestCandidate.DisplayHeader}”。",
                    Content = BuildMenuOperationContent(matchResult.BestCandidate, matchResult.Candidates, dryRun, "ambiguous"),
                    ErrorMessage = "请补充更具体的菜单名或完整路径。",
                };
            }

            var selectedCandidate = matchResult.BestCandidate;
            if (dryRun)
            {
                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"已完成菜单 dry-run：“{selectedCandidate.DisplayPath}”。",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "dry_run_only"),
                };
            }

            if (string.Equals(selectedCandidate.RiskLevel, CopilotMenuToolSupport.ConfirmationRequired, StringComparison.OrdinalIgnoreCase)
                && !allowConfirmationRequired)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"菜单“{selectedCandidate.DisplayPath}”需要用户确认，MCP 默认不会执行。",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "confirmation_required"),
                    ErrorMessage = "该菜单可能修改应用状态、文件、设备或运行流程；请由用户在界面中确认执行。",
                };
            }

            if (!selectedCandidate.CanExecute || selectedCandidate.MenuItem.Command?.CanExecute(null) != true)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"菜单“{selectedCandidate.DisplayPath}”当前不可执行。",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "cannot_execute"),
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

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"已调度执行菜单“{selectedCandidate.DisplayPath}”。",
                Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "scheduled"),
            };
        }

        public static async Task<CopilotCapabilityResult> SetThemeAsync(string? sourceText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CopilotApplicationControlSupport.TryResolveTheme(sourceText, out var targetTheme))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "未识别目标主题。",
                    Content = $"[可用主题] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
                    ErrorMessage = "请明确指定目标主题，例如深色、浅色、粉色、青色或跟随系统。",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法切换主题。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var targetThemeLabel = CopilotApplicationControlSupport.GetThemeDisplayName(targetTheme);
            var currentTheme = Theme.UseSystem;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                currentTheme = ThemeConfig.Instance.Theme;
                if (currentTheme == targetTheme)
                    return;

                ThemeConfig.Instance.Theme = targetTheme;
                Application.Current.ApplyTheme(targetTheme);
                ConfigService.Instance.SaveConfigs();
            });

            if (currentTheme == targetTheme)
            {
                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"当前已是 {targetThemeLabel} 主题，无需切换。",
                    Content = $"[当前主题] {targetThemeLabel}",
                };
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"已切换应用主题为 {targetThemeLabel}。",
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[已应用主题] {targetThemeLabel}",
                    $"[可用主题] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
                }),
            };
        }

        public static async Task<CopilotCapabilityResult> SetLanguageAsync(string? sourceText, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!CopilotApplicationControlSupport.TryResolveLanguage(sourceText, out var targetCulture))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "未识别目标语言。",
                    Content = $"[可用语言] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "请明确指定目标语言，例如中文、英文、zh-Hans 或 en-US。",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "当前没有可用的应用实例，无法切换语言。",
                    ErrorMessage = "Application.Current 为空。",
                };
            }

            var targetLanguageLabel = CopilotApplicationControlSupport.GetLanguageDisplayName(targetCulture);
            var currentCulture = string.Empty;
            var changed = false;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                currentCulture = Thread.CurrentThread.CurrentUICulture.Name;
                if (string.Equals(currentCulture, targetCulture, StringComparison.OrdinalIgnoreCase))
                    return;

                changed = LanguageManager.Current.LanguageChange(targetCulture);
            });

            if (string.Equals(currentCulture, targetCulture, StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"当前已是 {targetLanguageLabel}，无需切换。",
                    Content = $"[当前语言] {targetLanguageLabel}({targetCulture})",
                };
            }

            if (!changed)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"已取消切换到 {targetLanguageLabel}。",
                    Content = $"[可用语言] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "语言切换需要用户确认并重启应用；本次未完成变更。",
                };
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"已切换界面语言为 {targetLanguageLabel}，应用将重启。",
                Content = $"[目标语言] {targetLanguageLabel}({targetCulture})",
            };
        }

        private static string BuildCandidateList(System.Collections.Generic.IReadOnlyList<CopilotMenuToolSupport.MenuMatchCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "[候选菜单] 无";

            var lines = candidates
                .Take(5)
                .Select((candidate, index) => $"{index + 1}. {candidate.DisplayPath} [display_name={candidate.DisplayHeader}; source_type={candidate.SourceType}; risk_level={candidate.RiskLevel}; can_execute={candidate.CanExecute}]")
                .ToArray();

            return string.Join(Environment.NewLine, new[]
            {
                "[候选菜单]",
                string.Join(Environment.NewLine, lines),
            });
        }

        private static string BuildMenuOperationContent(
            CopilotMenuToolSupport.MenuMatchCandidate? selectedCandidate,
            System.Collections.Generic.IReadOnlyList<CopilotMenuToolSupport.MenuMatchCandidate> candidates,
            bool dryRun,
            string executionStatus)
        {
            var wouldExecute = selectedCandidate != null
                && selectedCandidate.CanExecute
                && string.Equals(selectedCandidate.RiskLevel, CopilotMenuToolSupport.LowRiskAction, StringComparison.OrdinalIgnoreCase);
            var lines = new System.Collections.Generic.List<string>
            {
                $"dry_run: {dryRun}",
                $"would_execute: {wouldExecute}",
                $"execution_status: {executionStatus}",
            };

            if (selectedCandidate != null)
            {
                lines.Add($"matched_menu_path: {selectedCandidate.DisplayPath}");
                lines.Add($"display_name: {selectedCandidate.DisplayHeader}");
                lines.Add($"source_type: {selectedCandidate.SourceType}");
                lines.Add($"risk_level: {selectedCandidate.RiskLevel}");
                lines.Add($"can_execute: {selectedCandidate.CanExecute}");
            }

            lines.Add(BuildCandidateList(candidates));
            return string.Join(Environment.NewLine, lines);
        }
    }
}