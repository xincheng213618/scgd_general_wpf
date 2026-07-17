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
                    List<ProjectDefinition> matches = ResolveDependency(
                        availableProjects,
                        project,
                        dependencyReference);
                    if (matches.Count == 0)
                    {
                        AddDiagnostic(new ProjectBuildDiagnostic(
                            ProjectBuildDiagnosticKind.MissingDependency,
                            project,
                            dependencyReference,
                            $"项目“{project.Name}”引用了不存在或未加入解决方案的依赖“{dependencyReference}”。"));
                        continue;
                    }
                    if (matches.Count > 1)
                    {
                        AddDiagnostic(new ProjectBuildDiagnostic(
                            ProjectBuildDiagnosticKind.AmbiguousDependency,
                            project,
                            dependencyReference,
                            $"项目“{project.Name}”的依赖“{dependencyReference}”匹配了多个项目，请引用具体 .cvproj 文件。"));
                        continue;
                    }

                    Visit(matches[0]);
                }
                stack.RemoveAt(stack.Count - 1);
                states[projectIdentity] = VisitState.Visited;
                if (!orderedProjects.Any(item => string.Equals(
                    GetProjectIdentity(item),
                    projectIdentity,
                    StringComparison.OrdinalIgnoreCase)))
                {
                    orderedProjects.Add(project);
                }
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

        private static List<ProjectDefinition> ResolveDependency(
            IReadOnlyList<ProjectDefinition> projects,
            ProjectDefinition owner,
            string dependencyReference)
        {
            string baseDirectory = owner.ProjectFile.Directory?.FullName
                ?? owner.ProjectDirectory.FullName;
            return projects
                .Where(project => SolutionExplorer.ProjectReferenceMatches(
                    baseDirectory,
                    dependencyReference,
                    project))
                .ToList();
        }

        private static string GetProjectIdentity(ProjectDefinition project)
        {
            return Path.GetFullPath(project.ProjectFile.FullName);
        }
    }
}
