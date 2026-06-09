#pragma warning disable CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI.Languages;

namespace ColorVision.Copilot
{
    internal static class CopilotApplicationControlSupport
    {
        private static readonly string[] ChangeKeywords =
        {
            "切换",
            "切到",
            "切成",
            "改成",
            "改为",
            "设置",
            "设为",
            "换成",
            "换到",
            "使用",
            "switch",
            "change",
            "set",
            "use",
            "apply",
        };

        private static readonly string[] ThemeSubjectKeywords =
        {
            "主题",
            "theme",
            "外观",
            "appearance",
            "配色",
        };

        private static readonly string[] LanguageSubjectKeywords =
        {
            "语言",
            "language",
            "locale",
            "culture",
            "界面语言",
            "ui language",
        };

        private static readonly IReadOnlyDictionary<Theme, IReadOnlyList<string>> ThemeAliases =
            new Dictionary<Theme, IReadOnlyList<string>>
            {
                [Theme.UseSystem] = new[]
                {
                    "跟随系统",
                    "系统主题",
                    "默认主题",
                    "use system",
                    "follow system",
                    "system theme",
                },
                [Theme.Light] = new[]
                {
                    "浅色",
                    "亮色",
                    "白色",
                    "light",
                    "light theme",
                },
                [Theme.Dark] = new[]
                {
                    "深色",
                    "暗色",
                    "黑色",
                    "dark",
                    "dark theme",
                },
                [Theme.Pink] = new[]
                {
                    "粉色",
                    "粉红",
                    "pink",
                    "pink theme",
                },
                [Theme.Cyan] = new[]
                {
                    "青色",
                    "青蓝",
                    "cyan",
                    "cyan theme",
                },
            };

        public static bool HasThemeIntent(string? text)
        {
            var normalizedText = NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
                return false;

            return ContainsAny(normalizedText, ThemeSubjectKeywords)
                || (ContainsAny(normalizedText, ChangeKeywords) && ThemeAliases.Values.SelectMany(aliases => aliases).Any(alias => ContainsAlias(normalizedText, alias)));
        }

        public static bool HasLanguageIntent(string? text)
        {
            var normalizedText = NormalizeText(text);
            if (string.IsNullOrWhiteSpace(normalizedText))
                return false;

            return ContainsAny(normalizedText, LanguageSubjectKeywords)
                || (ContainsAny(normalizedText, ChangeKeywords) && GetLanguageAliasMap().Values.SelectMany(aliases => aliases).Any(alias => ContainsAlias(normalizedText, alias)));
        }

        public static bool TryResolveTheme(string? text, out Theme theme)
        {
            var normalizedText = NormalizeText(text);
            foreach (var candidate in ThemeAliases
                .SelectMany(pair => pair.Value.Select(alias => (pair.Key, Alias: NormalizeText(alias))))
                .OrderByDescending(item => item.Alias.Length))
            {
                if (ContainsAlias(normalizedText, candidate.Alias))
                {
                    theme = candidate.Key;
                    return true;
                }
            }

            theme = default;
            return false;
        }

        public static bool TryResolveLanguage(string? text, out string cultureName)
        {
            var normalizedText = NormalizeText(text);
            foreach (var candidate in GetLanguageAliasMap()
                .SelectMany(pair => pair.Value.Select(alias => (CultureName: pair.Key, Alias: NormalizeText(alias))))
                .OrderByDescending(item => item.Alias.Length))
            {
                if (ContainsAlias(normalizedText, candidate.Alias))
                {
                    cultureName = candidate.CultureName;
                    return true;
                }
            }

            cultureName = string.Empty;
            return false;
        }

        public static string GetThemeDisplayName(Theme theme)
        {
            return theme switch
            {
                Theme.UseSystem => "System",
                Theme.Light => "Light",
                Theme.Dark => "Dark",
                Theme.Pink => "Pink",
                Theme.Cyan => "Cyan",
                _ => theme.ToString(),
            };
        }

        public static IReadOnlyList<string> GetThemeAliases(Theme theme)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                theme.ToString(),
                GetThemeDisplayName(theme),
            };

            if (ThemeAliases.TryGetValue(theme, out var aliasValues))
            {
                foreach (var alias in aliasValues)
                    aliases.Add(alias);
            }

            return aliases
                .Where(ShouldKeepAlias)
                .ToArray();
        }

        public static string GetThemeOptionsText()
        {
            return string.Join(", ", Enum.GetValues<Theme>().Select(theme => $"{GetThemeDisplayName(theme)}({theme})"));
        }

        public static IReadOnlyList<string> GetAvailableLanguages()
        {
            var languages = LanguageManager.GetLanguages();
            LanguageManager.Current.Languages = languages;
            return languages;
        }

        public static string GetLanguageDisplayName(string? cultureName)
        {
            var value = cultureName ?? string.Empty;
            _ = GetAvailableLanguages();

            try
            {
                return new CultureInfo(value).EnglishName;
            }
            catch (CultureNotFoundException)
            {
            }

            return value;
        }

        public static string GetLanguageOptionsText()
        {
            return string.Join(", ", GetAvailableLanguages().Select(language => $"{GetLanguageDisplayName(language)}({language})"));
        }

        public static IReadOnlyList<string> GetLanguageAliases(string? cultureName)
        {
            var value = cultureName ?? string.Empty;
            var aliasMap = GetLanguageAliasMap();
            return aliasMap.TryGetValue(value, out var aliases)
                ? aliases
                : Array.Empty<string>();
        }

        private static IReadOnlyDictionary<string, IReadOnlyList<string>> GetLanguageAliasMap()
        {
            var languages = GetAvailableLanguages();
            var aliasMap = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var language in languages)
            {
                var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    language,
                };

                if (LanguageManager.keyValuePairs != null
                    && LanguageManager.keyValuePairs.TryGetValue(language, out var displayName)
                    && !string.IsNullOrWhiteSpace(displayName))
                {
                    aliases.Add(displayName);
                }

                try
                {
                    var culture = new CultureInfo(language);
                    aliases.Add(culture.Name);
                    aliases.Add(culture.EnglishName);
                    aliases.Add(culture.DisplayName);
                    aliases.Add(culture.NativeName);
                }
                catch (CultureNotFoundException)
                {
                }

                foreach (var alias in GetManualLanguageAliases(language))
                    aliases.Add(alias);

                aliasMap[language] = aliases
                    .Where(ShouldKeepAlias)
                    .ToArray();
            }

            return aliasMap;
        }

        private static IEnumerable<string> GetManualLanguageAliases(string language)
        {
            if (language.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "中文",
                    "简体中文",
                    "简中",
                    "chinese",
                    "simplified chinese",
                };
            }

            if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "英文",
                    "英语",
                    "english",
                };
            }

            if (language.StartsWith("fr", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "法语",
                    "法文",
                    "french",
                };
            }

            if (language.StartsWith("de", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "德语",
                    "德文",
                    "german",
                };
            }

            if (language.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "日语",
                    "日文",
                    "japanese",
                };
            }

            if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
            {
                return new[]
                {
                    "韩语",
                    "韩文",
                    "korean",
                };
            }

            return Array.Empty<string>();
        }

        private static bool ShouldKeepAlias(string? alias)
        {
            var value = (alias ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.Length >= 3 || value.Any(ch => ch > 127);
        }

        private static bool ContainsAny(string normalizedText, IEnumerable<string> aliases)
        {
            return aliases.Any(alias => ContainsAlias(normalizedText, alias));
        }

        private static bool ContainsAlias(string normalizedText, string? alias)
        {
            var normalizedAlias = NormalizeText(alias);
            if (string.IsNullOrWhiteSpace(normalizedText) || string.IsNullOrWhiteSpace(normalizedAlias))
                return false;

            return normalizedText.Contains(normalizedAlias, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            return string.Join(" ", (text ?? string.Empty)
                .Trim()
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
                }, StringSplitOptions.RemoveEmptyEntries));
        }
    }
}
