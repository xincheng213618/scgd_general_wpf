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
                    Summary = "Missing documentation search question or keywords.",
                    ErrorMessage = "Provide a more specific software question, page name, or feature keyword.",
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
                        Summary = "No relevant snippets were found in the online documentation.",
                        ErrorMessage = "Make the question more specific, such as a feature name, page name, menu name, device name, or module name.",
                    };
                }

                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"Matched {searchResult.DistinctPageCount} documentation pages and returned {searchResult.Hits.Count} relevant snippets.",
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
                    Summary = "Could not read the online documentation index.",
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
            return ToCapabilityResult(snapshot);
        }

        public static async Task<CopilotCapabilityResult> CaptureAsync(
            string? query,
            CopilotRecentLogMode mode,
            int maxLines,
            int maxChars,
            CancellationToken cancellationToken)
        {
            var snapshot = await CopilotRecentLogSupport.CaptureAsync(query, mode, maxLines, maxChars, cancellationToken);
            return ToCapabilityResult(snapshot);
        }

        private static CopilotCapabilityResult ToCapabilityResult(CopilotRecentLogSnapshot snapshot)
        {
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
                    $"[Log File] {snapshot.FilePath}",
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
                    Summary = "No application instance is available, so the menu cannot be executed.",
                    ErrorMessage = "Application.Current is null.",
                };
            }

            var matchResult = await Application.Current.Dispatcher.InvokeAsync(() => CopilotMenuToolSupport.Resolve(sourceText));
            if (matchResult.BestCandidate == null || !matchResult.HasStrongMatch)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "No executable menu match was found.",
                    Content = BuildMenuOperationContent(null, matchResult.Suggestions, dryRun, "not_found"),
                    ErrorMessage = "Use a more specific menu name, such as Options, VAM, or Check for Updates.",
                };
            }

            if (matchResult.IsAmbiguous)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"Matched multiple candidate menus; did not execute {matchResult.BestCandidate.DisplayHeader}.",
                    Content = BuildMenuOperationContent(matchResult.BestCandidate, matchResult.Candidates, dryRun, "ambiguous"),
                    ErrorMessage = "Provide a more specific menu name or full menu path.",
                };
            }

            var selectedCandidate = matchResult.BestCandidate;
            if (dryRun)
            {
                return new CopilotCapabilityResult
                {
                    Success = true,
                    Summary = $"Completed menu dry-run: {selectedCandidate.DisplayPath}.",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "dry_run_only"),
                };
            }

            if (string.Equals(selectedCandidate.RiskLevel, CopilotMenuToolSupport.ConfirmationRequired, StringComparison.OrdinalIgnoreCase)
                && !allowConfirmationRequired)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"Menu {selectedCandidate.DisplayPath} requires user confirmation and will not be executed by MCP by default.",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "confirmation_required"),
                    ErrorMessage = "This menu may modify application state, files, devices, or flow execution. Ask the user to confirm it in the UI.",
                };
            }

            if (!selectedCandidate.CanExecute || selectedCandidate.MenuItem.Command?.CanExecute(null) != true)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"Menu {selectedCandidate.DisplayPath} is not executable right now.",
                    Content = BuildMenuOperationContent(selectedCandidate, matchResult.Candidates, dryRun, "cannot_execute"),
                    ErrorMessage = "The menu may be limited by permissions, current state, or context.",
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
                Summary = $"Scheduled menu execution: {selectedCandidate.DisplayPath}.",
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
                    Summary = "Target theme was not recognized.",
                    Content = $"[Available Themes] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
                    ErrorMessage = "Specify a target theme such as system, dark, light, pink, or cyan.",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "No application instance is available, so the theme cannot be changed.",
                    ErrorMessage = "Application.Current is null.",
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
                    Summary = $"The current theme is already {targetThemeLabel}; no change was needed.",
                    Content = $"[Current Theme] {targetThemeLabel}",
                };
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"Switched application theme to {targetThemeLabel}.",
                Content = string.Join(Environment.NewLine, new[]
                {
                    $"[Applied Theme] {targetThemeLabel}",
                    $"[Available Themes] {CopilotApplicationControlSupport.GetThemeOptionsText()}",
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
                    Summary = "Target language was not recognized.",
                    Content = $"[Available Languages] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "Specify a target language such as Chinese, English, zh-Hans, or en-US.",
                };
            }

            if (Application.Current == null)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "No application instance is available, so the language cannot be changed.",
                    ErrorMessage = "Application.Current is null.",
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
                    Summary = $"The current language is already {targetLanguageLabel}; no change was needed.",
                    Content = $"[Current Language] {targetLanguageLabel}({targetCulture})",
                };
            }

            if (!changed)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = $"Switch to {targetLanguageLabel} was cancelled.",
                    Content = $"[Available Languages] {CopilotApplicationControlSupport.GetLanguageOptionsText()}",
                    ErrorMessage = "Language switching requires user confirmation and an application restart; the change was not completed.",
                };
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"Switched UI language to {targetLanguageLabel}; the application will restart.",
                Content = $"[Target Language] {targetLanguageLabel}({targetCulture})",
            };
        }

        private static string BuildCandidateList(System.Collections.Generic.IReadOnlyList<CopilotMenuToolSupport.MenuMatchCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return "[Candidate Menus] None";

            var lines = candidates
                .Take(5)
                .Select((candidate, index) => $"{index + 1}. {candidate.DisplayPath} [display_name={candidate.DisplayHeader}; source_type={candidate.SourceType}; risk_level={candidate.RiskLevel}; can_execute={candidate.CanExecute}]")
                .ToArray();

            return string.Join(Environment.NewLine, new[]
            {
                "[Candidate Menus]",
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
