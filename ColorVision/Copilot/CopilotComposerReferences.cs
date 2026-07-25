using ColorVision.Engine.Templates;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    public enum CopilotComposerReferenceKind
    {
        Template,
        Menu,
        File,
    }

    public sealed class CopilotComposerReferenceItem
    {
        public CopilotComposerReferenceKind Kind { get; init; }

        public string Title { get; init; } = string.Empty;

        public string Subtitle { get; init; } = string.Empty;

        public string Value { get; init; } = string.Empty;

        public string SourceId { get; init; } = string.Empty;

        public string ContextContent { get; init; } = string.Empty;

        public string KindLabel => Kind switch
        {
            CopilotComposerReferenceKind.Template => "模板",
            CopilotComposerReferenceKind.Menu => "菜单",
            _ => "文件",
        };

        public string IconGlyph => Kind switch
        {
            CopilotComposerReferenceKind.Template => "\uE8A5",
            CopilotComposerReferenceKind.Menu => "\uE700",
            _ => "\uE8A5",
        };

        internal string SearchText => string.Join(" ", Title, Subtitle, Value);
    }

    internal readonly record struct CopilotComposerMention(int StartIndex, string Query);

    internal static class CopilotComposerReferenceCatalog
    {
        private const int MaximumIndexedWorkspaceFiles = 5000;
        private const int MaximumSuggestions = 12;
        private static readonly object FileIndexLock = new();
        private static readonly HashSet<string> SkippedDirectoryNames = new(StringComparer.OrdinalIgnoreCase)
        {
            ".git", ".vs", ".idea", ".codex-artifacts", "bin", "obj", "packages", "node_modules",
        };
        private static readonly HashSet<string> SupportedFileExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".props", ".targets", ".sln", ".slnx", ".xaml", ".xml", ".json", ".json5",
            ".yaml", ".yml", ".toml", ".ini", ".config", ".md", ".txt", ".csv", ".tsv", ".sql", ".py",
            ".ps1", ".bat", ".cmd", ".html", ".htm", ".css", ".js", ".ts", ".tsx", ".jsx", ".vue",
            ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".svg", ".pdf",
        };
        private static string _cachedWorkspaceRoot = string.Empty;
        private static IReadOnlyList<WorkspaceFileReference> _cachedWorkspaceFiles = Array.Empty<WorkspaceFileReference>();

        public static bool TryParseMention(string? input, out CopilotComposerMention mention)
        {
            var text = input ?? string.Empty;
            for (var index = text.Length - 1; index >= 0; index--)
            {
                if (text[index] != '@')
                    continue;
                if (index > 0 && !char.IsWhiteSpace(text[index - 1]))
                    continue;

                var suffix = text[(index + 1)..];
                if (suffix.Contains('\r') || suffix.Contains('\n') || suffix.Length > 80)
                    break;
                if (suffix.StartsWith('[') && suffix.Contains(']'))
                    break;

                mention = new CopilotComposerMention(index, suffix.Trim());
                return true;
            }

            mention = default;
            return false;
        }

        public static string CompleteMention(string? input, CopilotComposerMention mention, string title)
        {
            var text = input ?? string.Empty;
            if (mention.StartIndex < 0 || mention.StartIndex > text.Length)
                return text;

            var safeTitle = (title ?? string.Empty)
                .Replace("[", "(", StringComparison.Ordinal)
                .Replace("]", ")", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return text[..mention.StartIndex] + $"@[{safeTitle}] ";
        }

        public static IReadOnlyList<CopilotComposerReferenceItem> Search(
            string? query,
            string? activeDocumentPath,
            string? workspaceRoot)
        {
            var normalizedQuery = (query ?? string.Empty).Trim();
            var candidates = new List<CopilotComposerReferenceItem>();
            AddActiveDocument(candidates, activeDocumentPath);
            AddTemplates(candidates);
            AddMenus(candidates, normalizedQuery);
            AddWorkspaceFiles(candidates, workspaceRoot, normalizedQuery);

            var unique = candidates
                .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Title))
                .GroupBy(candidate => $"{candidate.Kind}:{candidate.Value}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (normalizedQuery.Length == 0)
            {
                var balanced = Enum.GetValues<CopilotComposerReferenceKind>()
                    .SelectMany(kind => unique
                        .Where(candidate => candidate.Kind == kind)
                        .Take(MaximumSuggestions / 3))
                    .ToList();
                balanced.AddRange(unique
                    .Where(candidate => !balanced.Contains(candidate))
                    .Take(MaximumSuggestions - balanced.Count));
                return balanced;
            }

            return unique
                .Select(candidate => (Candidate: candidate, Score: Score(normalizedQuery, candidate.SearchText)))
                .Where(item => item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Candidate.Kind)
                .ThenBy(item => item.Candidate.Title, StringComparer.OrdinalIgnoreCase)
                .Take(MaximumSuggestions)
                .Select(item => item.Candidate)
                .ToArray();
        }

        internal static IReadOnlyList<string> SearchWorkspaceFiles(string workspaceRoot, string query, int maximumResults = 8)
        {
            return SearchWorkspaceFileReferences(workspaceRoot, query, maximumResults)
                .Select(file => file.FullPath)
                .ToArray();
        }

        private static IReadOnlyList<WorkspaceFileReference> SearchWorkspaceFileReferences(
            string workspaceRoot,
            string query,
            int maximumResults)
        {
            return GetWorkspaceFiles(workspaceRoot)
                .Select(file => (File: file, Score: Score(query, file.RelativePath)))
                .Where(item => string.IsNullOrWhiteSpace(query) || item.Score > 0)
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.File.RelativePath.Length)
                .ThenBy(item => item.File.RelativePath, StringComparer.OrdinalIgnoreCase)
                .Take(Math.Max(1, maximumResults))
                .Select(item => item.File)
                .ToArray();
        }

        private static void AddActiveDocument(List<CopilotComposerReferenceItem> candidates, string? activeDocumentPath)
        {
            if (string.IsNullOrWhiteSpace(activeDocumentPath) || !File.Exists(activeDocumentPath))
                return;

            var fullPath = Path.GetFullPath(activeDocumentPath);
            candidates.Add(CreateFileReference(fullPath, "当前编辑文件"));
        }

        private static void AddTemplates(List<CopilotComposerReferenceItem> candidates)
        {
            try
            {
                foreach (var template in TemplateControl.ITemplateNames.Values
                    .Where(template => template != null)
                    .Distinct()
                    .Take(256))
                {
                    var title = FirstNonEmpty(template.Title, template.Name, template.Code, template.GetType().Name);
                    var code = FirstNonEmpty(template.Code, template.Name, template.GetType().Name);
                    candidates.Add(new CopilotComposerReferenceItem
                    {
                        Kind = CopilotComposerReferenceKind.Template,
                        Title = title,
                        Subtitle = $"模板代码 {code} · 字典 {template.TemplateDicId}",
                        Value = code,
                        SourceId = "composer-template:" + NormalizeSourceId(code),
                        ContextContent = string.Join(Environment.NewLine, new[]
                        {
                            "[ColorVision template reference]",
                            $"Title: {title}",
                            $"Code: {code}",
                            $"Template dictionary id: {template.TemplateDicId}",
                            "Use this as the template explicitly referenced by the user. Inspect it before proposing or applying changes.",
                        }),
                    });
                }
            }
            catch
            {
                // Template discovery is optional while the application is still initializing.
            }
        }

        private static void AddMenus(List<CopilotComposerReferenceItem> candidates, string query)
        {
            try
            {
                var result = CopilotMenuToolSupport.Resolve(query);
                var menus = query.Length == 0 ? result.Suggestions : result.Candidates;
                foreach (var menu in menus.Take(6))
                {
                    candidates.Add(new CopilotComposerReferenceItem
                    {
                        Kind = CopilotComposerReferenceKind.Menu,
                        Title = menu.DisplayHeader,
                        Subtitle = menu.DisplayPath,
                        Value = menu.DisplayPath,
                        SourceId = "composer-menu:" + NormalizeSourceId(menu.DisplayPath),
                        ContextContent = string.Join(Environment.NewLine, new[]
                        {
                            "[ColorVision menu reference]",
                            $"Menu path: {menu.DisplayPath}",
                            $"Menu title: {menu.DisplayHeader}",
                            $"Risk classification: {menu.RiskLevel}",
                            "The user referenced this menu. Do not execute it unless the request explicitly asks for that action.",
                        }),
                    });
                }
            }
            catch
            {
                // Menu discovery is optional while the main window is still initializing.
            }
        }

        private static void AddWorkspaceFiles(List<CopilotComposerReferenceItem> candidates, string? workspaceRoot, string query)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot))
                return;

            foreach (var file in SearchWorkspaceFileReferences(workspaceRoot, query, maximumResults: 10))
            {
                candidates.Add(CreateFileReference(
                    file.FullPath,
                    file.RelativePath));
            }
        }

        private static CopilotComposerReferenceItem CreateFileReference(string fullPath, string subtitle)
        {
            return new CopilotComposerReferenceItem
            {
                Kind = CopilotComposerReferenceKind.File,
                Title = Path.GetFileName(fullPath),
                Subtitle = subtitle,
                Value = fullPath,
                SourceId = "composer-file:" + NormalizeSourceId(fullPath),
            };
        }

        private static IReadOnlyList<WorkspaceFileReference> GetWorkspaceFiles(string? workspaceRoot)
        {
            if (string.IsNullOrWhiteSpace(workspaceRoot) || !Directory.Exists(workspaceRoot))
                return Array.Empty<WorkspaceFileReference>();

            string normalizedRoot;
            try
            {
                normalizedRoot = Path.GetFullPath(workspaceRoot);
            }
            catch
            {
                return Array.Empty<WorkspaceFileReference>();
            }

            lock (FileIndexLock)
            {
                if (string.Equals(_cachedWorkspaceRoot, normalizedRoot, StringComparison.OrdinalIgnoreCase))
                    return _cachedWorkspaceFiles;

                _cachedWorkspaceRoot = normalizedRoot;
                _cachedWorkspaceFiles = BuildWorkspaceFileIndex(normalizedRoot);
                return _cachedWorkspaceFiles;
            }
        }

        private static List<WorkspaceFileReference> BuildWorkspaceFileIndex(string workspaceRoot)
        {
            var files = new List<WorkspaceFileReference>();
            var pendingDirectories = new Stack<string>();
            pendingDirectories.Push(workspaceRoot);

            while (pendingDirectories.Count > 0 && files.Count < MaximumIndexedWorkspaceFiles)
            {
                var directory = pendingDirectories.Pop();
                try
                {
                    foreach (var filePath in Directory.EnumerateFiles(directory))
                    {
                        if (!SupportedFileExtensions.Contains(Path.GetExtension(filePath)))
                            continue;

                        files.Add(new WorkspaceFileReference(
                            Path.GetFullPath(filePath),
                            Path.GetRelativePath(workspaceRoot, filePath)));
                        if (files.Count >= MaximumIndexedWorkspaceFiles)
                            break;
                    }

                    foreach (var childDirectory in Directory.EnumerateDirectories(directory))
                    {
                        var name = Path.GetFileName(childDirectory);
                        if (SkippedDirectoryNames.Contains(name))
                            continue;
                        if ((File.GetAttributes(childDirectory) & FileAttributes.ReparsePoint) != 0)
                            continue;

                        pendingDirectories.Push(childDirectory);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
                {
                }
            }

            return files;
        }

        private static int Score(string query, string text)
        {
            if (string.IsNullOrWhiteSpace(query))
                return 1;

            var normalizedQuery = query.Trim();
            var normalizedText = text ?? string.Empty;
            if (normalizedText.Equals(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                return 1000;
            if (normalizedText.StartsWith(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                return 800;
            if (normalizedText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase))
                return 600;

            var terms = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            return terms.Length > 0 && terms.All(term => normalizedText.Contains(term, StringComparison.OrdinalIgnoreCase))
                ? 400 + terms.Length
                : 0;
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

        private static string NormalizeSourceId(string value)
        {
            var normalized = new string((value ?? string.Empty)
                .Where(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
                .Take(72)
                .ToArray());
            return normalized.Length == 0 ? "reference" : normalized;
        }

        private sealed record WorkspaceFileReference(string FullPath, string RelativePath);
    }
}
