using System.IO;

namespace ColorVision.Solution.Explorer
{
    public enum ProjectBuildDiagnosticKind
    {
        MissingDependency,
        AmbiguousDependency,
        CircularDependency,
    }

    public sealed record ProjectBuildDiagnostic(
        ProjectBuildDiagnosticKind Kind,
        ProjectDefinition Project,
        string Reference,
        string Message);

    public sealed record ProjectBuildPlan(
        IReadOnlyList<ProjectDefinition> OrderedProjects,
        IReadOnlyList<ProjectBuildDiagnostic> Diagnostics)
    {
        public bool IsValid => Diagnostics.Count == 0;

        public string FormatDiagnostics()
        {
            return string.Join(Environment.NewLine, Diagnostics.Select(diagnostic => $"• {diagnostic.Message}"));
        }
    }

    /// <summary>
    /// Produces a deterministic dependency-first project order. Dependency
    /// references are resolved relative to the project file, matching common
    /// project-system behavior rather than the current process directory.
    /// </summary>
    public static class ProjectBuildPlanner
    {
        private enum VisitState
        {
            Visiting,
            Visited,
        }

        public static ProjectBuildPlan Create(
            IEnumerable<ProjectDefinition> projects,
            IEnumerable<ProjectDefinition>? targets = null)
        {
            List<ProjectDefinition> availableProjects = projects
                .GroupBy(GetProjectIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(project => project.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(project => project.ProjectFile.FullName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<ProjectDefinition> requestedProjects = (targets ?? availableProjects)
                .GroupBy(GetProjectIdentity, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
            var stack = new List<ProjectDefinition>();
            var orderedProjects = new List<ProjectDefinition>();
            var diagnostics = new List<ProjectBuildDiagnostic>();
            var diagnosticKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, ProjectDefinition?>? projectsByPath = null;

            foreach (ProjectDefinition project in requestedProjects)
                Visit(project);

            return new ProjectBuildPlan(orderedProjects, diagnostics);

            void Visit(ProjectDefinition project)
            {
                string projectIdentity = GetProjectIdentity(project);
                if (states.TryGetValue(projectIdentity, out VisitState state))
                {
                    if (state == VisitState.Visiting)
                        AddCycleDiagnostic(project);
                    return;
                }

                states[projectIdentity] = VisitState.Visiting;
                stack.Add(project);
                foreach (string dependencyReference in project.Dependencies ?? Array.Empty<string>())
                {
                    string? referencePath = ResolveDependencyPath(project, dependencyReference);
                    if (referencePath == null ||
                        !(projectsByPath ??= IndexProjectsByPath(availableProjects)).TryGetValue(referencePath, out ProjectDefinition? match))
                    {
                        AddDiagnostic(new ProjectBuildDiagnostic(
                            ProjectBuildDiagnosticKind.MissingDependency,
                            project,
                            dependencyReference,
                            $"项目“{project.Name}”引用了不存在或未加入解决方案的依赖“{dependencyReference}”。"));
                        continue;
                    }
                    if (match == null)
                    {
                        AddDiagnostic(new ProjectBuildDiagnostic(
                            ProjectBuildDiagnosticKind.AmbiguousDependency,
                            project,
                            dependencyReference,
                            $"项目“{project.Name}”的依赖“{dependencyReference}”匹配了多个项目，请引用具体 .cvproj 文件。"));
                        continue;
                    }

                    Visit(match);
                }
                stack.RemoveAt(stack.Count - 1);
                states[projectIdentity] = VisitState.Visited;
                orderedProjects.Add(project);
            }

            void AddCycleDiagnostic(ProjectDefinition repeatedProject)
            {
                string repeatedIdentity = GetProjectIdentity(repeatedProject);
                int cycleStart = stack.FindIndex(item => string.Equals(
                    GetProjectIdentity(item),
                    repeatedIdentity,
                    StringComparison.OrdinalIgnoreCase));
                IEnumerable<ProjectDefinition> cycleProjects = cycleStart >= 0
                    ? stack.Skip(cycleStart).Append(repeatedProject)
                    : stack.Append(repeatedProject);
                string cycle = string.Join(" → ", cycleProjects.Select(item => item.Name));
                AddDiagnostic(new ProjectBuildDiagnostic(
                    ProjectBuildDiagnosticKind.CircularDependency,
                    repeatedProject,
                    repeatedProject.ProjectFile.FullName,
                    $"检测到项目循环依赖：{cycle}。"));
            }

            void AddDiagnostic(ProjectBuildDiagnostic diagnostic)
            {
                string key = $"{diagnostic.Kind}\n{diagnostic.Project.ProjectFile.FullName}\n{diagnostic.Reference}";
                if (diagnosticKeys.Add(key))
                    diagnostics.Add(diagnostic);
            }
        }

        private static Dictionary<string, ProjectDefinition?> IndexProjectsByPath(IReadOnlyList<ProjectDefinition> projects)
        {
            var index = new Dictionary<string, ProjectDefinition?>(StringComparer.OrdinalIgnoreCase);
            foreach (ProjectDefinition project in projects)
            {
                AddPath(project.ProjectFile.FullName, project);
                AddPath(project.ProjectDirectory.FullName, project);
            }
            return index;

            void AddPath(string path, ProjectDefinition project)
            {
                // A null entry marks a path shared by different projects.
                if (!index.TryAdd(path, project) && index[path] is { } existing && !ReferenceEquals(existing, project))
                    index[path] = null;
            }
        }

        private static string? ResolveDependencyPath(ProjectDefinition owner, string dependencyReference)
        {
            string baseDirectory = owner.ProjectFile.Directory?.FullName
                ?? owner.ProjectDirectory.FullName;
            if (string.IsNullOrWhiteSpace(dependencyReference)) return null;
            try
            {
                return SolutionExplorer.GetFullProjectReferencePath(baseDirectory, dependencyReference);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return null;
            }
        }

        private static string GetProjectIdentity(ProjectDefinition project)
        {
            return Path.GetFullPath(project.ProjectFile.FullName);
        }
    }
}
