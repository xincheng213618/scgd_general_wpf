using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotCodexRuntimeLocator
    {
        // Desktop releases stage their executable here; the hash directory changes on update.
        internal static IReadOnlyList<string> FindCandidates(
            string? localAppData = null, string? appData = null, string? path = null, string? programFiles = null)
        {
            localAppData ??= Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            appData ??= Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            path ??= Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            programFiles ??= Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new List<string>();
            var desktopRoot = Path.Combine(localAppData, "OpenAI", "Codex", "bin");
            candidates.AddRange(Directories(desktopRoot)
                .OrderByDescending(directory => Directory.GetLastWriteTimeUtc(directory))
                .Select(directory => Path.Combine(directory, "codex.exe")));
            candidates.Add(Path.Combine(localAppData, "Programs", "Codex", "resources", "codex.exe"));

            foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                var expanded = Environment.ExpandEnvironmentVariables(directory.Trim().Trim('"'));
                // Never search the working directory or execute a shell shim from a project.
                if (Path.IsPathFullyQualified(expanded) && !expanded.StartsWith(@"\\", StringComparison.Ordinal))
                    candidates.Add(Path.Combine(expanded, "codex.exe"));
            }

            var npmRoot = Path.Combine(appData, "npm", "node_modules", "@openai");
            foreach (var package in Directories(npmRoot).Where(p => Path.GetFileName(p).StartsWith("codex", StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var target in Directories(Path.Combine(package, "vendor")))
                {
                    candidates.Add(Path.Combine(target, "codex", "codex.exe"));
                    candidates.Add(Path.Combine(target, "bin", "codex.exe"));
                }
                foreach (var dependency in Directories(Path.Combine(package, "node_modules", "@openai")))
                    foreach (var target in Directories(Path.Combine(dependency, "vendor")))
                    {
                        candidates.Add(Path.Combine(target, "codex", "codex.exe"));
                        candidates.Add(Path.Combine(target, "bin", "codex.exe"));
                    }
            }

            // A newly installed Store app may not have staged its runtime until its first launch.
            foreach (var package in Directories(Path.Combine(programFiles, "WindowsApps"))
                .Where(p => Path.GetFileName(p).StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)))
                candidates.Add(Path.Combine(package, "app", "resources", "codex.exe"));

            return candidates.Where(File.Exists).Select(Path.GetFullPath)
                .Distinct(StringComparer.OrdinalIgnoreCase).Take(16).ToArray();
        }

        private static string[] Directories(string path)
        {
            try { return Directory.Exists(path) ? Directory.GetDirectories(path) : []; }
            catch (IOException) { return []; }
            catch (UnauthorizedAccessException) { return []; }
        }
    }
}
