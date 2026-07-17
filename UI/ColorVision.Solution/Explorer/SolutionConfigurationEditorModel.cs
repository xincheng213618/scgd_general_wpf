using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace ColorVision.Solution.Explorer
{
    public enum SolutionConfigurationDiagnosticSeverity
    {
        Warning,
        Error,
    }

    public sealed record SolutionConfigurationDiagnostic(
        SolutionConfigurationDiagnosticSeverity Severity,
        string Message);

    public sealed record SolutionConfigurationChanges(
        string ActiveConfiguration,
        string ActivePlatform,
        ProjectDefinition? StartupProject,
        IReadOnlyDictionary<string, Dictionary<string, string>> ProjectConfigurations,
        IReadOnlyDictionary<string, IReadOnlyList<string>> ProjectDependencies)
    {
        public SolutionConfigurationChanges(
            string activeConfiguration,
            ProjectDefinition? startupProject,
            IReadOnlyDictionary<string, Dictionary<string, string>> projectConfigurations,
            IReadOnlyDictionary<string, IReadOnlyList<string>> projectDependencies)
            : this(
                activeConfiguration,
                SolutionConfigurationIdentity.DefaultPlatform,
                startupProject,
                projectConfigurations,
                projectDependencies)
        {
        }
    }

    public sealed class SolutionStartupProjectOption
    {
        public ProjectDefinition? Project { get; }
        public string DisplayName { get; }

        public SolutionStartupProjectOption(ProjectDefinition? project, string displayName)
        {
            Project = project;
            DisplayName = displayName;
        }
    }

    public sealed class SolutionDependencyOption : INotifyPropertyChanged
    {
        private readonly Action _changed;
        private bool _isSelected;

        public ProjectDefinition? Project { get; }
        public string Reference { get; }
        public string DisplayName { get; }
        public bool IsAvailable { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value)
                    return;
                _isSelected = value;
                OnPropertyChanged();
                _changed();
            }
        }

        internal SolutionDependencyOption(
            ProjectDefinition? project,
            string reference,
            string displayName,
            bool isAvailable,
            bool isSelected,
            Action changed)
        {
            Project = project;
            Reference = reference;
            DisplayName = displayName;
            IsAvailable = isAvailable;
            _isSelected = isSelected;
            _changed = changed;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class SolutionConfigurationProjectModel : INotifyPropertyChanged
    {
        private readonly Action _changed;
        private string _selectedConfiguration;

        public ProjectDefinition Project { get; }
        public string ProjectReference { get; }
        public string DisplayName => string.IsNullOrWhiteSpace(Project.LoadError)
            ? Project.Name
            : $"{Project.Name} (加载失败)";
        public ObservableCollection<string> AvailableConfigurations { get; } = new();
        public ObservableCollection<SolutionDependencyOption> Dependencies { get; } = new();
        public bool CanEditDependencies { get; }

        public string SelectedConfiguration
        {
            get => _selectedConfiguration;
            set
            {
                string normalizedValue = string.IsNullOrWhiteSpace(value) ? "Debug" : value.Trim();
                if (string.Equals(_selectedConfiguration, normalizedValue, StringComparison.Ordinal))
                    return;
                _selectedConfiguration = normalizedValue;
                OnPropertyChanged();
                _changed();
            }
        }

        internal SolutionConfigurationProjectModel(
            ProjectDefinition project,
            string projectReference,
            string selectedConfiguration,
            Action changed)
        {
            Project = project;
            ProjectReference = projectReference;
            _selectedConfiguration = selectedConfiguration;
            _changed = changed;
            CanEditDependencies = ProjectProviderRegistry.CanChangeProjectDependencies(project);
        }

        internal void SetSelectedConfiguration(string value)
        {
            _selectedConfiguration = value;
            OnPropertyChanged(nameof(SelectedConfiguration));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public sealed class SolutionConfigurationEditorModel : INotifyPropertyChanged
    {
        private readonly string _solutionDirectory;
        private readonly Dictionary<string, Dictionary<string, string>> _draftMappings;
        private bool _suppressChanges;
        private string _activeConfiguration;
        private string _activePlatform;
        private SolutionConfigurationProjectModel? _selectedProject;
        private SolutionStartupProjectOption? _selectedStartupProject;
        private string? _unavailableStartupReference;

        public ObservableCollection<string> AvailableConfigurations { get; }
        public ObservableCollection<string> AvailablePlatforms { get; }
        public ObservableCollection<SolutionConfigurationProjectModel> Projects { get; } = new();
        public ObservableCollection<SolutionStartupProjectOption> StartupProjects { get; } = new();
        public ObservableCollection<SolutionConfigurationDiagnostic> Diagnostics { get; } = new();

        public string ActiveConfiguration
        {
            get => _activeConfiguration;
            set => ChangeActiveConfiguration(value);
        }

        public string ActivePlatform
        {
            get => _activePlatform;
            set => ChangeActivePlatform(value);
        }

        public SolutionConfigurationProjectModel? SelectedProject
        {
            get => _selectedProject;
            set
            {
                if (ReferenceEquals(_selectedProject, value))
                    return;
                _selectedProject = value;
                OnPropertyChanged();
            }
        }

        public SolutionStartupProjectOption? SelectedStartupProject
        {
            get => _selectedStartupProject;
            set
            {
                if (ReferenceEquals(_selectedStartupProject, value))
                    return;
                _selectedStartupProject = value;
                if (!_suppressChanges)
                    _unavailableStartupReference = null;
                OnPropertyChanged();
                Changed();
            }
        }

        public bool HasErrors => Diagnostics.Any(diagnostic =>
            diagnostic.Severity == SolutionConfigurationDiagnosticSeverity.Error);
        public bool HasDiagnostics => Diagnostics.Count > 0;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ModelChanged;

        public SolutionConfigurationEditorModel(
            string solutionDirectory,
            IEnumerable<ProjectDefinition> projects,
            string? activeConfiguration,
            string? startupProjectReference,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations)
            : this(
                solutionDirectory,
                projects,
                activeConfiguration,
                SolutionConfigurationIdentity.DefaultPlatform,
                startupProjectReference,
                projectConfigurations)
        {
        }

        public SolutionConfigurationEditorModel(
            string solutionDirectory,
            IEnumerable<ProjectDefinition> projects,
            string? activeConfiguration,
            string? activePlatform,
            string? startupProjectReference,
            IReadOnlyDictionary<string, Dictionary<string, string>>? projectConfigurations)
        {
            _solutionDirectory = solutionDirectory;
            _activeConfiguration = SolutionExplorer.NormalizeConfigurationName(activeConfiguration);
            _activePlatform = SolutionExplorer.NormalizePlatformName(activePlatform);
            _draftMappings = CloneMappings(projectConfigurations);
            _suppressChanges = true;
            List<ProjectDefinition> availableProjects = projects
                .GroupBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            AvailableConfigurations = new ObservableCollection<string>(
                SolutionExplorer.GetAvailableSolutionConfigurations(
                    availableProjects,
                    _activeConfiguration,
                    _draftMappings));
            AvailablePlatforms = new ObservableCollection<string>(
                SolutionExplorer.GetAvailableSolutionPlatforms(
                    _activePlatform,
                    _draftMappings));

            foreach (ProjectDefinition project in availableProjects)
            {
                string projectReference = Path.GetRelativePath(_solutionDirectory, project.ProjectFile.FullName)
                    .Replace('\\', '/');
                string selectedConfiguration = SolutionExplorer.ResolveProjectConfigurationName(
                    _solutionDirectory,
                    _activeConfiguration,
                    _activePlatform,
                    _draftMappings,
                    project);
                Projects.Add(new SolutionConfigurationProjectModel(
                    project,
                    projectReference,
                    selectedConfiguration,
                    Changed));
            }

            BuildDependencyOptions();
            BuildStartupOptions(startupProjectReference);
            RefreshProjectConfigurationOptions();
            SelectedProject = Projects.FirstOrDefault();
            _suppressChanges = false;
            Validate();
        }

        public SolutionConfigurationChanges CreateChanges()
        {
            StoreCurrentMappings();
            var dependencies = Projects.ToDictionary(
                project => project.Project.ProjectFile.FullName,
                project => (IReadOnlyList<string>)project.Dependencies
                    .Where(dependency => dependency.IsSelected)
                    .Select(dependency => dependency.Reference)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);
            return new SolutionConfigurationChanges(
                _activeConfiguration,
                _activePlatform,
                _selectedStartupProject?.Project,
                CloneMappings(_draftMappings),
                dependencies);
        }

        public void Validate()
        {
            Diagnostics.Clear();
            foreach (SolutionConfigurationProjectModel projectModel in Projects)
            {
                ProjectDefinition project = projectModel.Project;
                bool hasBaseCommands = project.Commands?.Count > 0;
                bool hasSelectedConfiguration = project.Configurations?.Keys.Any(configuration =>
                    string.Equals(
                        configuration,
                        projectModel.SelectedConfiguration,
                        StringComparison.OrdinalIgnoreCase)) == true;
                if (!hasBaseCommands && project.Configurations?.Count > 0 && !hasSelectedConfiguration)
                {
                    Diagnostics.Add(new SolutionConfigurationDiagnostic(
                        SolutionConfigurationDiagnosticSeverity.Error,
                        $"项目“{project.Name}”不存在配置“{projectModel.SelectedConfiguration}”。"));
                }
            }

            if (!string.IsNullOrWhiteSpace(_unavailableStartupReference))
            {
                Diagnostics.Add(new SolutionConfigurationDiagnostic(
                    SolutionConfigurationDiagnosticSeverity.Error,
                    $"启动项目“{_unavailableStartupReference}”不可用，请重新选择启动项目。"));
            }
            else if (_selectedStartupProject?.Project is { } startupProject)
            {
                SolutionConfigurationProjectModel startupModel = Projects.First(project =>
                    PathEquals(project.Project.ProjectFile.FullName, startupProject.ProjectFile.FullName));
                ProjectDefinition configuredStartupProject = startupProject.ForConfiguration(
                    startupModel.SelectedConfiguration);
                if (!ProjectProviderRegistry.HasCapability(configuredStartupProject, ProjectCapabilityIds.Run)
                    && !ProjectProviderRegistry.HasCapability(configuredStartupProject, ProjectCapabilityIds.Debug))
                {
                    Diagnostics.Add(new SolutionConfigurationDiagnostic(
                        SolutionConfigurationDiagnosticSeverity.Error,
                        $"启动项目“{startupProject.Name}”在配置“{startupModel.SelectedConfiguration}”下没有 Run 或 Debug 命令。"));
                }
            }

            List<ProjectDefinition> projectedProjects = Projects
                .Select(CreateProjectedProject)
                .ToList();
            ProjectBuildPlan plan = ProjectBuildPlanner.Create(projectedProjects);
            foreach (ProjectBuildDiagnostic diagnostic in plan.Diagnostics)
            {
                Diagnostics.Add(new SolutionConfigurationDiagnostic(
                    SolutionConfigurationDiagnosticSeverity.Error,
                    diagnostic.Message));
            }

            OnPropertyChanged(nameof(HasErrors));
            OnPropertyChanged(nameof(HasDiagnostics));
        }

        public static string CreateDependencyReference(
            ProjectDefinition owner,
            ProjectDefinition dependency)
        {
            string ownerDirectory = owner.ProjectFile.Directory?.FullName
                ?? owner.ProjectDirectory.FullName;
            return Path.GetRelativePath(ownerDirectory, dependency.ProjectFile.FullName)
                .Replace('\\', '/');
        }

        private void ChangeActiveConfiguration(string? configurationName)
        {
            string normalizedName = SolutionExplorer.NormalizeConfigurationName(configurationName);
            if (string.Equals(_activeConfiguration, normalizedName, StringComparison.OrdinalIgnoreCase))
                return;

            StoreCurrentMappings();
            _activeConfiguration = normalizedName;
            if (!AvailableConfigurations.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
                AvailableConfigurations.Add(normalizedName);
            _suppressChanges = true;
            foreach (SolutionConfigurationProjectModel project in Projects)
            {
                project.SetSelectedConfiguration(SolutionExplorer.ResolveProjectConfigurationName(
                    _solutionDirectory,
                    _activeConfiguration,
                    _activePlatform,
                    _draftMappings,
                    project.Project));
            }
            _suppressChanges = false;
            RefreshProjectConfigurationOptions();
            OnPropertyChanged(nameof(ActiveConfiguration));
            Changed();
        }

        private void ChangeActivePlatform(string? platformName)
        {
            string normalizedName = SolutionExplorer.NormalizePlatformName(platformName);
            if (string.Equals(_activePlatform, normalizedName, StringComparison.OrdinalIgnoreCase))
                return;

            StoreCurrentMappings();
            _activePlatform = normalizedName;
            if (!AvailablePlatforms.Contains(normalizedName, StringComparer.OrdinalIgnoreCase))
                AvailablePlatforms.Add(normalizedName);
            _suppressChanges = true;
            foreach (SolutionConfigurationProjectModel project in Projects)
            {
                project.SetSelectedConfiguration(SolutionExplorer.ResolveProjectConfigurationName(
                    _solutionDirectory,
                    _activeConfiguration,
                    _activePlatform,
                    _draftMappings,
                    project.Project));
            }
            _suppressChanges = false;
            RefreshProjectConfigurationOptions();
            OnPropertyChanged(nameof(ActivePlatform));
            Changed();
        }

        private void StoreCurrentMappings()
        {
            foreach (SolutionConfigurationProjectModel project in Projects)
            {
                string mappingReference = _draftMappings.Keys.FirstOrDefault(reference =>
                    SolutionExplorer.ProjectReferenceMatches(
                        _solutionDirectory,
                        reference,
                        project.Project)) ?? project.ProjectReference;
                if (!_draftMappings.TryGetValue(mappingReference, out Dictionary<string, string>? mapping))
                {
                    mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _draftMappings[mappingReference] = mapping;
                }

                string configurationKey = SolutionConfigurationIdentity.CreateKey(
                    _activeConfiguration,
                    _activePlatform);
                foreach (string existingConfiguration in mapping.Keys
                    .Where(configuration => string.Equals(
                            configuration,
                            configurationKey,
                            StringComparison.OrdinalIgnoreCase)
                        || (string.Equals(
                                _activePlatform,
                                SolutionConfigurationIdentity.DefaultPlatform,
                                StringComparison.OrdinalIgnoreCase)
                            && string.Equals(
                                configuration,
                                _activeConfiguration,
                                StringComparison.OrdinalIgnoreCase)))
                    .ToList())
                {
                    mapping.Remove(existingConfiguration);
                }
                if (!string.Equals(
                    project.SelectedConfiguration,
                    _activeConfiguration,
                    StringComparison.OrdinalIgnoreCase))
                {
                    mapping[configurationKey] = project.SelectedConfiguration;
                }
                if (mapping.Count == 0)
                    _draftMappings.Remove(mappingReference);
            }
        }

        private void BuildStartupOptions(string? startupProjectReference)
        {
            StartupProjects.Add(new SolutionStartupProjectOption(null, "（无启动项目）"));
            foreach (SolutionConfigurationProjectModel project in Projects)
                StartupProjects.Add(new SolutionStartupProjectOption(project.Project, project.DisplayName));

            if (!string.IsNullOrWhiteSpace(startupProjectReference))
            {
                _selectedStartupProject = StartupProjects.FirstOrDefault(option =>
                    option.Project != null
                    && SolutionExplorer.ProjectReferenceMatches(
                        _solutionDirectory,
                        startupProjectReference,
                        option.Project));
                if (_selectedStartupProject == null)
                {
                    _selectedStartupProject = StartupProjects[0];
                    _unavailableStartupReference = startupProjectReference;
                }
            }
            _selectedStartupProject ??= StartupProjects[0];
            OnPropertyChanged(nameof(SelectedStartupProject));
        }

        private void BuildDependencyOptions()
        {
            foreach (SolutionConfigurationProjectModel owner in Projects)
            {
                var optionsByProject = new Dictionary<string, SolutionDependencyOption>(StringComparer.OrdinalIgnoreCase);
                foreach (SolutionConfigurationProjectModel candidate in Projects.Where(candidate =>
                    !PathEquals(candidate.Project.ProjectFile.FullName, owner.Project.ProjectFile.FullName)))
                {
                    string reference = CreateDependencyReference(owner.Project, candidate.Project);
                    var option = new SolutionDependencyOption(
                        candidate.Project,
                        reference,
                        candidate.DisplayName,
                        isAvailable: true,
                        isSelected: false,
                        Changed);
                    optionsByProject[candidate.Project.ProjectFile.FullName] = option;
                    owner.Dependencies.Add(option);
                }

                foreach (string existingReference in owner.Project.Dependencies ?? Array.Empty<string>())
                {
                    List<SolutionConfigurationProjectModel> matches = Projects.Where(candidate =>
                        SolutionExplorer.ProjectReferenceMatches(
                            owner.Project.ProjectFile.Directory?.FullName ?? owner.Project.ProjectDirectory.FullName,
                            existingReference,
                            candidate.Project)).ToList();
                    if (matches.Count == 1
                        && optionsByProject.TryGetValue(
                            matches[0].Project.ProjectFile.FullName,
                            out SolutionDependencyOption? option))
                    {
                        option.IsSelected = true;
                        continue;
                    }

                    string suffix = matches.Count == 0 ? "缺失" : "不明确";
                    owner.Dependencies.Insert(0, new SolutionDependencyOption(
                        project: null,
                        existingReference,
                        $"{existingReference} ({suffix})",
                        isAvailable: false,
                        isSelected: true,
                        Changed));
                }
            }
        }

        private void RefreshProjectConfigurationOptions()
        {
            foreach (SolutionConfigurationProjectModel project in Projects)
            {
                var configurations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (project.Project.Configurations != null)
                {
                    foreach (string configuration in project.Project.Configurations.Keys)
                        configurations.Add(configuration);
                }
                if (project.Project.Commands?.Count > 0 || configurations.Count == 0)
                    configurations.Add(_activeConfiguration);
                configurations.Add(project.SelectedConfiguration);

                project.AvailableConfigurations.Clear();
                foreach (string configuration in configurations
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase))
                {
                    project.AvailableConfigurations.Add(configuration);
                }
            }
        }

        private ProjectDefinition CreateProjectedProject(SolutionConfigurationProjectModel project)
        {
            IReadOnlyList<string> dependencies = project.Dependencies
                .Where(dependency => dependency.IsSelected)
                .Select(dependency => dependency.Reference)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            return (project.Project with { Dependencies = dependencies })
                .ForConfiguration(project.SelectedConfiguration);
        }

        private void Changed()
        {
            if (_suppressChanges)
                return;
            Validate();
            ModelChanged?.Invoke(this, EventArgs.Empty);
        }

        private static Dictionary<string, Dictionary<string, string>> CloneMappings(
            IReadOnlyDictionary<string, Dictionary<string, string>>? mappings)
        {
            var result = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            if (mappings == null)
                return result;
            foreach (var projectMapping in mappings)
            {
                result[projectMapping.Key] = projectMapping.Value == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(projectMapping.Value, StringComparer.OrdinalIgnoreCase);
            }
            return result;
        }

        private static bool PathEquals(string left, string right)
        {
            return string.Equals(
                Path.GetFullPath(left),
                Path.GetFullPath(right),
                StringComparison.OrdinalIgnoreCase);
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
