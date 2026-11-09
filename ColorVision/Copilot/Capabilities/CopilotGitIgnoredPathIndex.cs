using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace ColorVision.Copilot
{
    // Lets workspace searches skip what the repository itself declares as not source, such as SDK
    // copies and build output, which otherwise exhaust the scan limit and make every search partial.
    // Git owns the ignore semantics; without a usable Git the search simply covers more files.
    internal static class CopilotGitIgnoredPathIndex
    {
        private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(30);
        private const int GitTimeoutMilliseconds = 5000;
        private static readonly object CacheLock = new();
        private static readonly Dictionary<string, (long ExpiresAt, HashSet<string> Paths)> Cache = new(StringComparer.OrdinalIgnoreCase);

        internal static IReadOnlySet<string> GetIgnoredPaths(string searchRoot)
        {
            var repositoryRoot = FindRepositoryRoot(searchRoot);
            if (repositoryRoot.Length == 0)
                return EmptySet;

            lock (CacheLock)
            {
                if (Cache.TryGetValue(repositoryRoot, out var cached) && cached.ExpiresAt > Environment.TickCount64)
                    return cached.Paths;
            }

            var paths = ListIgnoredPaths(repositoryRoot);
            lock (CacheLock)
                Cache[repositoryRoot] = (Environment.TickCount64 + (long)CacheLifetime.TotalMilliseconds, paths);
            return paths;
        }

        private static readonly HashSet<string> EmptySet = new(StringComparer.OrdinalIgnoreCase);

        private static string FindRepositoryRoot(string searchRoot)
        {
            for (var current = searchRoot; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current) ?? string.Empty)
            {
                var marker = Path.Combine(current, ".git");
                if (Directory.Exists(marker) || File.Exists(marker))
                    return current;
            }
            return string.Empty;
        }

        private static HashSet<string> ListIgnoredPaths(string repositoryRoot)
        {
            var gitExecutable = CopilotGitProcessSupport.FindTrustedGitExecutable();
            if (string.IsNullOrWhiteSpace(gitExecutable))
                return EmptySet;

            var startInfo = new ProcessStartInfo(gitExecutable)
            {
                WorkingDirectory = repositoryRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var argument in new[]
            {
                "--no-pager", "--no-optional-locks",
                "-c", "core.quotepath=false",
                "-c", "core.fsmonitor=false",
                "-c", "core.untrackedCache=false",
                "-c", "core.worktree=" + repositoryRoot,
                "ls-files", "--others", "--ignored", "--exclude-standard", "--directory", "-z",
            })
            {
                startInfo.ArgumentList.Add(argument);
            }
            foreach (var (name, value) in CopilotGitProcessSupport.EnvironmentOverrides)
                startInfo.Environment[name] = value;

            try
            {
                using var process = Process.Start(startInfo);
                if (process == null)
                    return EmptySet;
                process.ErrorDataReceived += static (_, _) => { };
                process.BeginErrorReadLine();
                var output = process.StandardOutput.ReadToEndAsync();
                if (!process.WaitForExit(GitTimeoutMilliseconds))
                {
                    process.Kill(entireProcessTree: true);
                    return EmptySet;
                }
                if (process.ExitCode != 0 || !output.Wait(GitTimeoutMilliseconds))
                    return EmptySet;

                var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var relative in output.Result.Split('\0', StringSplitOptions.RemoveEmptyEntries))
                {
                    var full = Path.GetFullPath(relative, repositoryRoot);
                    paths.Add(Path.TrimEndingDirectorySeparator(full));
                }
                return paths;
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or UnauthorizedAccessException or ArgumentException or AggregateException)
            {
                return EmptySet;
            }
        }
    }
}
