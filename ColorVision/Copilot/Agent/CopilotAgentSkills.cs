using Microsoft.Agents.AI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotAgentSkills : IDisposable
    {
        internal const int MaxActiveSkills = 16;
        internal const int MaxAdvertisedSkillCharacters = 8_000;
        internal const int SkillMetadataContextPercent = 2;
        private const int EstimatedCharactersPerToken = 4;
        private readonly BudgetedAgentSkillsSource? _budgetedSource;
        private readonly int _metadataCharacterBudget;

        private CopilotAgentSkills(
            IReadOnlyList<string> searchPaths,
            BudgetedAgentSkillsSource? budgetedSource,
            AgentSkillsSource? source,
            int metadataCharacterBudget)
        {
            SearchPaths = searchPaths;
            _budgetedSource = budgetedSource;
            Source = source;
            _metadataCharacterBudget = metadataCharacterBudget;
        }

        public IReadOnlyList<string> SearchPaths { get; }

        public AgentSkillsSource? Source { get; }

        public bool IsEnabled => Source != null;

        internal static CopilotAgentSkills Disabled() => new([], null, null, 0);

        public static CopilotAgentSkills Create(
            CopilotAgentRequest request,
            IEnumerable<string>? historicalExplicitOnlySkillNames = null,
            int contextWindowTokens = CopilotAgentTokenBudget.DefaultContextWindowTokens,
            string? applicationBaseDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            var searchPaths = ResolveSearchPaths(request, applicationBaseDirectory);
            if (searchPaths.Count == 0)
                return Disabled();

            AgentSkillsSource source = new AgentFileSkillsSource(
                searchPaths,
                scriptRunner: null,
                options: new AgentFileSkillsSourceOptions
                {
                    SearchDepth = 2,
                    ScriptFilter = _ => false,
                },
                loggerFactory: null);
            source = new DeduplicatingAgentSkillsSource(source, loggerFactory: null);
            var metadataCharacterBudget = ResolveMetadataCharacterBudget(contextWindowTokens);
            var budgetedSource = new BudgetedAgentSkillsSource(
                source,
                request.UserText,
                historicalExplicitOnlySkillNames,
                MaxActiveSkills,
                metadataCharacterBudget);
            source = new CachingAgentSkillsSource(budgetedSource, new CachingAgentSkillsSourceOptions());
            return new CopilotAgentSkills(searchPaths, budgetedSource, source, metadataCharacterBudget);
        }

        internal static int ResolveMetadataCharacterBudget(int contextWindowTokens)
        {
            var boundedContextTokens = Math.Max(1, contextWindowTokens);
            var proportionalCharacters = (long)boundedContextTokens * EstimatedCharactersPerToken * SkillMetadataContextPercent / 100;
            return (int)Math.Clamp(proportionalCharacters, 1, MaxAdvertisedSkillCharacters);
        }

        public string BuildStartupDiagnostic()
        {
            return $"Agent Skills enabled · up to {MaxActiveSkills} relevant skill(s) and {_metadataCharacterBudget:N0} metadata characters ({SkillMetadataContextPercent}% context, {MaxAdvertisedSkillCharacters:N0} hard cap) from {SearchPaths.Count} trusted root(s) · scripts disabled.";
        }

        public string? BuildSelectionDiagnostic()
        {
            var snapshot = _budgetedSource?.GetSnapshot();
            if (snapshot == null || !snapshot.DiscoveryCompleted)
                return null;
            if (snapshot.DiscoveredCount == 0)
                return "Agent Skills selected · no valid project or built-in skills were discovered.";

            var builder = new StringBuilder()
                .Append("Agent Skills selected · ")
                .Append(snapshot.SelectedNames.Count)
                .Append('/')
                .Append(snapshot.DiscoveredCount)
                .Append(" active");
            var omittedCount = snapshot.DiscoveredCount - snapshot.SelectedNames.Count;
            var explicitOnlyCount = snapshot.MetadataExplicitOnlyNames.Count + snapshot.HistoricalExplicitOnlyNames.Count;
            var budgetOmittedCount = omittedCount - explicitOnlyCount;
            if (explicitOnlyCount > 0)
            {
                builder.Append(" · ").Append(explicitOnlyCount).Append(" explicit-only");
                if (snapshot.MetadataExplicitOnlyNames.Count > 0)
                    builder.Append(" (policy ").Append(snapshot.MetadataExplicitOnlyNames.Count).Append(')');
                if (snapshot.HistoricalExplicitOnlyNames.Count > 0)
                    builder.Append(" (low-use ").Append(snapshot.HistoricalExplicitOnlyNames.Count).Append(')');
            }
            if (budgetOmittedCount > 0)
                builder.Append(" · ").Append(budgetOmittedCount).Append(" omitted by the active-skill budget");
            if (snapshot.ShortenedDescriptionNames.Count > 0)
                builder.Append(" · ").Append(snapshot.ShortenedDescriptionNames.Count).Append(" description(s) shortened by the metadata budget");
            if (snapshot.LoadedNames.Length == 0)
                builder.Append(" · none loaded this run.");
            else
                builder.Append(" · ").Append(snapshot.LoadedNames.Length).Append(" loaded this run: ").AppendJoin(", ", snapshot.LoadedNames).Append('.');
            return builder.ToString();
        }

        public bool TryGetRunUsage(out IReadOnlyList<string> selectedNames, out IReadOnlyList<string> loadedNames)
        {
            var snapshot = _budgetedSource?.GetSnapshot();
            if (snapshot == null || !snapshot.DiscoveryCompleted || snapshot.SelectedNames.Count == 0)
            {
                selectedNames = Array.Empty<string>();
                loadedNames = Array.Empty<string>();
                return false;
            }

            selectedNames = snapshot.SelectedNames;
            loadedNames = snapshot.LoadedNames;
            return true;
        }

        internal static IReadOnlyList<string> ResolveSearchPaths(CopilotAgentRequest request, string? applicationBaseDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            var paths = new List<string>();
            foreach (var root in request.SearchRootPaths ?? Array.Empty<string>())
                AddExistingSkillRoot(paths, TryGetDirectory(root), Path.Combine(".agents", "skills"));

            var baseDirectory = string.IsNullOrWhiteSpace(applicationBaseDirectory)
                ? AppContext.BaseDirectory
                : applicationBaseDirectory;
            AddExistingSkillRoot(paths, baseDirectory, Path.Combine("Copilot", "Skills"));
            return paths;
        }

        public void Dispose()
        {
            Source?.Dispose();
        }

        private static string? TryGetDirectory(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return Directory.Exists(fullPath)
                    ? fullPath
                    : File.Exists(fullPath) ? Path.GetDirectoryName(fullPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static void AddExistingSkillRoot(List<string> paths, string? parentDirectory, string relativePath)
        {
            if (string.IsNullOrWhiteSpace(parentDirectory))
                return;

            try
            {
                var candidate = Path.GetFullPath(Path.Combine(parentDirectory, relativePath));
                if (!Directory.Exists(candidate)
                    || (File.GetAttributes(candidate) & FileAttributes.ReparsePoint) != 0
                    || paths.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                {
                    return;
                }
                paths.Add(candidate);
            }
            catch
            {
            }
        }

        private sealed class BudgetedAgentSkillsSource : DelegatingAgentSkillsSource
        {
            private readonly object _sync = new();
            private readonly string _userText;
            private readonly int _maximumCount;
            private readonly int _maximumMetadataCharacters;
            private readonly HashSet<string> _historicalExplicitOnlySkillNames;
            private readonly HashSet<string> _loadedNames = new(StringComparer.OrdinalIgnoreCase);
            private SkillSelectionSnapshot _snapshot = SkillSelectionSnapshot.Empty;

            public BudgetedAgentSkillsSource(
                AgentSkillsSource innerSource,
                string? userText,
                IEnumerable<string>? historicalExplicitOnlySkillNames,
                int maximumCount,
                int maximumMetadataCharacters)
                : base(innerSource)
            {
                _userText = userText ?? string.Empty;
                _historicalExplicitOnlySkillNames = (historicalExplicitOnlySkillNames ?? Array.Empty<string>())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                _maximumCount = maximumCount;
                _maximumMetadataCharacters = maximumMetadataCharacters;
            }

            public override async Task<IList<AgentSkill>> GetSkillsAsync(
                AgentSkillsSourceContext context,
                CancellationToken cancellationToken = default)
            {
                var discovered = await InnerSource.GetSkillsAsync(context, cancellationToken).ConfigureAwait(false);
                var selection = CopilotAgentSkillSelectionPolicy.Select(
                    discovered.ToArray(),
                    _userText,
                    _historicalExplicitOnlySkillNames,
                    _maximumCount,
                    _maximumMetadataCharacters);
                var selectedNames = selection.SelectedSkills.Select(skill => skill.Frontmatter.Name).ToArray();
                lock (_sync)
                {
                    _snapshot = new SkillSelectionSnapshot(
                        true,
                        discovered.Count,
                        selectedNames,
                        GetLoadedNames(selectedNames),
                        selection.MetadataExplicitOnlyNames,
                        selection.HistoricalExplicitOnlyNames,
                        selection.ShortenedDescriptionNames);
                }
                return selection.SelectedSkills.Select(skill => (AgentSkill)new TrackingAgentSkill(skill, TrackLoad)).ToArray();
            }

            public SkillSelectionSnapshot GetSnapshot()
            {
                lock (_sync)
                {
                    return _snapshot with { LoadedNames = GetLoadedNames(_snapshot.SelectedNames) };
                }
            }

            private void TrackLoad(string name)
            {
                lock (_sync)
                {
                    _loadedNames.Add(name);
                }
            }

            private string[] GetLoadedNames(IReadOnlyList<string> selectedNames)
            {
                return selectedNames.Where(_loadedNames.Contains).ToArray();
            }
        }

        private sealed class TrackingAgentSkill : AgentSkill
        {
            private readonly AgentSkill _inner;
            private readonly Action<string> _trackLoad;

            public TrackingAgentSkill(AgentSkill inner, Action<string> trackLoad)
            {
                _inner = inner;
                _trackLoad = trackLoad;
            }

            public override AgentSkillFrontmatter Frontmatter => _inner.Frontmatter;

            public override async ValueTask<string> GetContentAsync(CancellationToken cancellationToken = default)
            {
                var content = await _inner.GetContentAsync(cancellationToken).ConfigureAwait(false);
                _trackLoad(Frontmatter.Name);
                return content;
            }

            public override async ValueTask<AgentSkillResource?> GetResourceAsync(string name, CancellationToken cancellationToken = default)
            {
                var resource = await _inner.GetResourceAsync(name, cancellationToken).ConfigureAwait(false);
                if (resource != null)
                    _trackLoad(Frontmatter.Name);
                return resource;
            }

            public override ValueTask<AgentSkillScript?> GetScriptAsync(string name, CancellationToken cancellationToken = default)
            {
                return _inner.GetScriptAsync(name, cancellationToken);
            }
        }

        private sealed record SkillSelectionSnapshot(
            bool DiscoveryCompleted,
            int DiscoveredCount,
            IReadOnlyList<string> SelectedNames,
            string[] LoadedNames,
            IReadOnlyList<string> MetadataExplicitOnlyNames,
            IReadOnlyList<string> HistoricalExplicitOnlyNames,
            IReadOnlyList<string> ShortenedDescriptionNames)
        {
            public static SkillSelectionSnapshot Empty { get; } = new(false, 0, [], [], [], [], []);
        }

    }
}
