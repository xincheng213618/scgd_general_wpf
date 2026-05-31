using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ColorVision.Themes;
using ColorVision.UI.Menus;

namespace ColorVision.Copilot
{
    internal static class CopilotMenuToolSupport
    {
        private const int StrongMatchScore = 700;
        private const int AmbiguousScoreGap = 60;
        public const string LowRiskAction = "low-risk-action";
        public const string ConfirmationRequired = "confirmation-required";

        private static readonly Regex HotkeySuffixRegex = new(@"\((_|&).\)", RegexOptions.Compiled);
        private static readonly string[] IntentKeywords =
        {
            "打开",
            "进入",
            "显示",
            "查看",
            "启动",
            "运行",
            "执行",
            "调用",
            "切换",
            "切到",
            "切成",
            "改成",
            "改为",
            "设为",
            "设置",
            "检查",
            "打开菜单",
            "菜单",
            "open",
            "show",
            "launch",
            "run",
            "execute",
            "trigger",
            "switch",
            "change",
            "set",
            "check",
            "menu",
        };

        private static readonly string[] ConfirmationRequiredTerms =
        {
            "删除",
            "移除",
            "清空",
            "保存",
            "导出",
            "导入",
            "更新",
            "安装",
            "卸载",
            "启动",
            "运行",
            "执行",
            "停止",
            "重启",
            "复位",
            "校准",
            "采集",
            "拍照",
            "下载",
            "上传",
            "delete",
            "remove",
            "clear",
            "save",
            "export",
            "import",
            "update",
            "install",
            "uninstall",
            "start",
            "run",
            "execute",
            "stop",
            "restart",
            "reset",
            "calibration",
            "calibrate",
            "capture",
            "acquire",
            "download",
            "upload",
        };

        private static readonly string[] LowRiskTerms =
        {
            "打开",
            "显示",
            "查看",
            "日志",
            "帮助",
            "关于",
            "选项",
            "设置",
            "配置",
            "主题",
            "语言",
            "文档",
            "面板",
            "copilot",
            "open",
            "show",
            "view",
            "log",
            "help",
            "about",
            "option",
            "options",
            "setting",
            "settings",
            "config",
            "theme",
            "language",
            "document",
            "panel",
        };

        private static readonly string[] SearchStopWords =
        {
            "打开",
            "进入",
            "显示",
            "查看",
            "启动",
            "运行",
            "执行",
            "调用",
            "切换",
            "切到",
            "切成",
            "改成",
            "改为",
            "设为",
            "设置",
            "检查",
            "打开菜单",
            "菜单",
            "界面",
            "应用",
            "软件",
            "系统",
            "please",
            "open",
            "show",
            "launch",
            "run",
            "execute",
            "trigger",
            "switch",
            "change",
            "set",
            "check",
            "menu",
            "theme",
            "language",
            "ui",
        };

        private static readonly string[] PathStopWords =
        {
            "主菜单",
            "菜单",
            "menu",
        };

        internal sealed class MenuMatchCandidate
        {
            public IMenuItem MenuItem { get; init; } = null!;

            public string DisplayHeader { get; init; } = string.Empty;

            public string DisplayPath { get; init; } = string.Empty;

            public string SourceType { get; init; } = string.Empty;

            public int Score { get; init; }

            public bool CanExecute { get; init; }

            public string RiskLevel { get; init; } = ConfirmationRequired;
        }

        internal sealed class MenuMatchResult
        {
            public MenuMatchCandidate? BestCandidate { get; init; }

            public IReadOnlyList<MenuMatchCandidate> Candidates { get; init; } = Array.Empty<MenuMatchCandidate>();

            public IReadOnlyList<MenuMatchCandidate> Suggestions { get; init; } = Array.Empty<MenuMatchCandidate>();

            public bool IsAmbiguous { get; init; }

            public bool HasStrongMatch => BestCandidate != null && BestCandidate.Score >= StrongMatchScore;
        }

        public static bool HasMenuIntent(string? text)
        {
            var normalizedText = NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
                return false;

            if (ContainsAny(normalizedText, IntentKeywords))
                return true;

            var searchText = BuildSearchText(normalizedText);
            return !string.IsNullOrWhiteSpace(searchText)
                && searchText.Length <= 40;
        }

        public static MenuMatchResult Resolve(string? text)
        {
            var normalizedText = NormalizeText(text);
            var searchTexts = BuildSearchTexts(normalizedText);
            var actionableMenus = GetActionableMenus();
            if (searchTexts.Count == 0 || actionableMenus.Count == 0)
            {
                return new MenuMatchResult
                {
                    Suggestions = actionableMenus
                        .Take(5)
                        .Select(candidate => new MenuMatchCandidate
                        {
                            MenuItem = candidate.MenuItem,
                            DisplayHeader = candidate.DisplayHeader,
                            DisplayPath = candidate.DisplayPath,
                            SourceType = candidate.SourceType,
                            Score = 0,
                            CanExecute = candidate.CanExecute,
                            RiskLevel = candidate.RiskLevel,
                        })
                        .ToArray(),
                };
            }

            var scoredCandidates = actionableMenus
                .Select(candidate => new MenuMatchCandidate
                {
                    MenuItem = candidate.MenuItem,
                    DisplayHeader = candidate.DisplayHeader,
                    DisplayPath = candidate.DisplayPath,
                    SourceType = candidate.SourceType,
                    Score = searchTexts.Max(searchText => ScoreCandidate(searchText, candidate.Aliases)),
                    CanExecute = candidate.CanExecute,
                    RiskLevel = candidate.RiskLevel,
                })
                .Where(candidate => candidate.Score > 0)
                .OrderByDescending(candidate => candidate.Score)
                .ThenBy(candidate => candidate.DisplayPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var bestCandidate = scoredCandidates.FirstOrDefault();
            var secondCandidate = scoredCandidates.Skip(1).FirstOrDefault();
            var isAmbiguous = bestCandidate != null
                && secondCandidate != null
                && bestCandidate.Score >= StrongMatchScore
                && secondCandidate.Score >= StrongMatchScore
                && bestCandidate.Score - secondCandidate.Score < AmbiguousScoreGap;

            return new MenuMatchResult
            {
                BestCandidate = bestCandidate,
                Candidates = scoredCandidates.Take(5).ToArray(),
                Suggestions = scoredCandidates.Take(5).ToArray(),
                IsAmbiguous = isAmbiguous,
            };
        }

        private static IReadOnlyList<ActionableMenuEntry> GetActionableMenus()
        {
            var allMenus = MenuManager.GetInstance()
                .GetAllMenuItemsFiltered()
                .Where(menuItem => menuItem.Visibility == System.Windows.Visibility.Visible)
                .Where(menuItem => menuItem.TargetName == MenuItemConstants.MainWindowTarget || menuItem.TargetName == MenuItemConstants.GlobalTarget)
                .ToArray();

            var parentHeaders = allMenus
                .Where(menuItem => !string.IsNullOrWhiteSpace(menuItem.GuidId))
                .GroupBy(menuItem => menuItem.GuidId!, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            return allMenus
                .Where(IsActionableMenu)
                .Select(menuItem => BuildActionableMenu(menuItem, parentHeaders))
                .Where(entry => entry != null)
                .Cast<ActionableMenuEntry>()
                .ToArray();
        }

        private static ActionableMenuEntry? BuildActionableMenu(IMenuItem menuItem, IReadOnlyDictionary<string, IMenuItem> parentHeaders)
        {
            var displayHeader = CleanDisplayText(menuItem.Header);
            if (string.IsNullOrWhiteSpace(displayHeader))
                return null;

            var displayPath = BuildDisplayPath(menuItem, parentHeaders, displayHeader);
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                NormalizeText(displayHeader),
                NormalizeText(displayPath),
            };

            if (!string.IsNullOrWhiteSpace(menuItem.GuidId))
                aliases.Add(NormalizeText(menuItem.GuidId));

            aliases.Add(NormalizeText(GetFriendlyTypeName(menuItem.GetType().Name)));

            foreach (var alias in GetExtraAliases(menuItem))
                aliases.Add(NormalizeText(alias));

            aliases.RemoveWhere(string.IsNullOrWhiteSpace);

            return new ActionableMenuEntry
            {
                MenuItem = menuItem,
                DisplayHeader = displayHeader,
                DisplayPath = displayPath,
                SourceType = GetSourceType(menuItem),
                Aliases = aliases.ToArray(),
                CanExecute = menuItem.Command?.CanExecute(null) ?? false,
                RiskLevel = ClassifyRisk(menuItem, displayHeader, displayPath),
            };
        }

        private static string GetSourceType(IMenuItem menuItem)
        {
            return menuItem.TargetName switch
            {
                MenuItemConstants.MainWindowTarget => "main-window-menu",
                MenuItemConstants.GlobalTarget => "global-menu",
                _ => string.IsNullOrWhiteSpace(menuItem.TargetName) ? "menu" : menuItem.TargetName,
            };
        }

        private static string ClassifyRisk(IMenuItem menuItem, string displayHeader, string displayPath)
        {
            var text = NormalizeText(string.Join(" ", new[]
            {
                displayHeader,
                displayPath,
                menuItem.GuidId ?? string.Empty,
                menuItem.OwnerGuid ?? string.Empty,
                GetFriendlyTypeName(menuItem.GetType().Name),
            }));

            if (ContainsAny(text, ConfirmationRequiredTerms))
                return ConfirmationRequired;

            if (ContainsAny(text, LowRiskTerms))
                return LowRiskAction;

            return ConfirmationRequired;
        }

        private static IEnumerable<string> GetExtraAliases(IMenuItem menuItem)
        {
            if (!string.IsNullOrWhiteSpace(menuItem.OwnerGuid)
                && string.Equals(menuItem.OwnerGuid, "MenuTheme", StringComparison.OrdinalIgnoreCase)
                && Enum.TryParse<Theme>(menuItem.GuidId, ignoreCase: true, out var theme))
            {
                foreach (var alias in CopilotApplicationControlSupport.GetThemeAliases(theme))
                    yield return alias;
            }

            if (!string.IsNullOrWhiteSpace(menuItem.OwnerGuid)
                && string.Equals(menuItem.OwnerGuid, "MenuLanguage", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(menuItem.GuidId))
            {
                foreach (var alias in CopilotApplicationControlSupport.GetLanguageAliases(menuItem.GuidId))
                    yield return alias;
            }

            if (!string.IsNullOrWhiteSpace(menuItem.Header))
                yield return menuItem.Header;

            if (!string.IsNullOrWhiteSpace(menuItem.GuidId))
                yield return menuItem.GuidId;
        }

        private static bool IsActionableMenu(IMenuItem menuItem)
        {
            if (menuItem.Command == null || string.IsNullOrWhiteSpace(menuItem.Header))
                return false;

            if (menuItem is not MenuItemBase menuItemBase)
                return true;

            var executeMethod = menuItemBase.GetType().GetMethod(nameof(MenuItemBase.Execute), BindingFlags.Instance | BindingFlags.Public);
            return executeMethod?.DeclaringType != typeof(MenuItemBase);
        }

        private static string BuildDisplayPath(IMenuItem menuItem, IReadOnlyDictionary<string, IMenuItem> parentHeaders, string displayHeader)
        {
            var segments = new Stack<string>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var ownerGuid = menuItem.OwnerGuid;

            while (!string.IsNullOrWhiteSpace(ownerGuid)
                && !string.Equals(ownerGuid, MenuItemConstants.Menu, StringComparison.OrdinalIgnoreCase)
                && visited.Add(ownerGuid))
            {
                if (!parentHeaders.TryGetValue(ownerGuid, out var parentMenu))
                    break;

                var parentHeader = CleanDisplayText(parentMenu.Header);
                if (!string.IsNullOrWhiteSpace(parentHeader)
                    && !PathStopWords.Contains(parentHeader, StringComparer.OrdinalIgnoreCase))
                {
                    segments.Push(parentHeader);
                }

                ownerGuid = parentMenu.OwnerGuid;
            }

            segments.Push(displayHeader);
            return string.Join(" > ", segments);
        }

        private static IReadOnlyList<string> BuildSearchTexts(string normalizedText)
        {
            var searchTexts = new List<string>();
            var primary = BuildSearchText(normalizedText);
            if (!string.IsNullOrWhiteSpace(primary))
                searchTexts.Add(primary);

            if (!string.IsNullOrWhiteSpace(normalizedText) && !searchTexts.Contains(normalizedText, StringComparer.OrdinalIgnoreCase))
                searchTexts.Add(normalizedText);

            return searchTexts
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static string BuildSearchText(string normalizedText)
        {
            if (string.IsNullOrWhiteSpace(normalizedText))
                return string.Empty;

            var tokens = normalizedText
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(token => !SearchStopWords.Contains(token, StringComparer.OrdinalIgnoreCase))
                .ToArray();

            return tokens.Length > 0
                ? string.Join(" ", tokens)
                : normalizedText;
        }

        private static int ScoreCandidate(string searchText, IReadOnlyList<string> aliases)
        {
            if (string.IsNullOrWhiteSpace(searchText) || aliases.Count == 0)
                return 0;

            var bestScore = 0;
            foreach (var alias in aliases)
            {
                if (string.IsNullOrWhiteSpace(alias))
                    continue;

                bestScore = Math.Max(bestScore, ScoreAlias(searchText, alias));
            }

            return bestScore;
        }

        private static int ScoreAlias(string searchText, string alias)
        {
            if (string.IsNullOrWhiteSpace(searchText) || string.IsNullOrWhiteSpace(alias))
                return 0;

            if (string.Equals(searchText, alias, StringComparison.OrdinalIgnoreCase))
                return 1000;

            if (searchText.Contains(alias, StringComparison.OrdinalIgnoreCase))
                return 820 + Math.Min(alias.Length * 6, 120);

            if (alias.Contains(searchText, StringComparison.OrdinalIgnoreCase))
                return 760 + Math.Min(searchText.Length * 6, 120);

            var searchTokens = searchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (searchTokens.Length > 0 && searchTokens.All(token => alias.Contains(token, StringComparison.OrdinalIgnoreCase)))
                return 660 + Math.Min(searchTokens.Sum(token => token.Length) * 5, 100);

            return 0;
        }

        private static string GetFriendlyTypeName(string typeName)
        {
            return CleanDisplayText(typeName
                .Replace("Menu", " Menu ", StringComparison.Ordinal)
                .Replace("Window", " Window ", StringComparison.Ordinal));
        }

        private static bool ContainsAny(string normalizedText, IEnumerable<string> values)
        {
            return values.Any(value => normalizedText.Contains(NormalizeText(value), StringComparison.OrdinalIgnoreCase));
        }

        private static string CleanDisplayText(string? text)
        {
            var value = text ?? string.Empty;
            value = HotkeySuffixRegex.Replace(value, string.Empty);
            value = value.Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("&", string.Empty, StringComparison.Ordinal)
                .Trim();

            return Regex.Replace(value, @"\s+", " ").Trim();
        }

        private static string NormalizeText(string? text)
        {
            var cleanedText = CleanDisplayText(text);
            if (string.IsNullOrWhiteSpace(cleanedText))
                return string.Empty;

            return string.Join(" ", cleanedText
                .ToLowerInvariant()
                .Replace("-", " ")
                .Replace("_", " ")
                .Split(new[]
                {
                    ' ',
                    '\r',
                    '\n',
                    '\t',
                    ',',
                    '.',
                    '，',
                    '。',
                    '!',
                    '！',
                    '?',
                    '？',
                    ':',
                    '：',
                    ';',
                    '；',
                    '/',
                    '\\',
                    '(',
                    ')',
                    '[',
                    ']',
                    '{',
                    '}',
                    '"',
                    '\'',
                    '>',
                }, StringSplitOptions.RemoveEmptyEntries));
        }

        private sealed class ActionableMenuEntry
        {
            public IMenuItem MenuItem { get; init; } = null!;

            public string DisplayHeader { get; init; } = string.Empty;

            public string DisplayPath { get; init; } = string.Empty;

            public string SourceType { get; init; } = string.Empty;

            public IReadOnlyList<string> Aliases { get; init; } = Array.Empty<string>();

            public bool CanExecute { get; init; }

            public string RiskLevel { get; init; } = ConfirmationRequired;
        }
    }
}