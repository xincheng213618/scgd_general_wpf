using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;

namespace ColorVision.Engine.Services.DeveloperTools
{
    /// <summary>Reads installation metadata without executing interpreters, launchers or shell profiles.</summary>
    public sealed class DeveloperToolDiscoveryService
    {
        public DeveloperToolSnapshot Inspect(DeveloperToolKind kind)
        {
            string executable = kind == DeveloperToolKind.Python ? "python.exe" : "node.exe";
            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            string registeredPath = string.Join(";",
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine),
                Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User));
            string currentCommand = ResolvePathCommand(currentPath, executable);
            string refreshedCommand = ResolvePathCommand(registeredPath, executable);
            var candidates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            AddPathCandidates(currentPath, executable, "应用 PATH", candidates);
            AddPathCandidates(registeredPath, executable, "系统 / 用户 PATH", candidates);
            if (kind == DeveloperToolKind.Python)
            {
                AddPythonRegistryCandidates(candidates);
                string pythonRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python");
                try
                {
                    if (Directory.Exists(pythonRoot))
                        foreach (string directory in Directory.EnumerateDirectories(pythonRoot).Take(64))
                            AddCandidate(Path.Combine(directory, executable), "常见安装目录", candidates);
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }
            else
            {
                foreach (string root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
                    AddCandidate(Path.Combine(root, "nodejs", executable), "常见安装目录", candidates);
                AddNodeRegistryCandidates(candidates);
            }

            var installations = new List<DeveloperToolInstallation>();
            foreach (var candidate in candidates.Take(128))
            {
                if (IsWindowsExecutionAlias(candidate.Key)) continue;
                try
                {
                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(candidate.Key);
                    string version = info.ProductVersion ?? info.FileVersion ?? "未知";
                    string directory = Path.GetDirectoryName(candidate.Key)!;
                    string packageVersion = kind == DeveloperToolKind.Python
                        ? ReadPipVersion(directory)
                        : ReadNpmVersion(directory);
                    installations.Add(new DeveloperToolInstallation(version, candidate.Key, candidate.Value, packageVersion));
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }

            string packageManagerPath = kind == DeveloperToolKind.NodeJs
                ? ResolvePathCommand(registeredPath, "npm.cmd")
                : "";
            string note = "版本来自文件元数据；检测不会运行脚本，也不会修改 PATH。";
            if (IsWindowsExecutionAlias(currentCommand) || IsWindowsExecutionAlias(refreshedCommand))
                note += " 检测到 Windows 应用执行别名，别名存在不代表解释器已安装。";
            if (kind == DeveloperToolKind.NodeJs && HasVersionManager())
                note += " 检测到 Node 版本管理器，请优先用原管理器升级，避免与 MSI 安装冲突。";
            if (!string.Equals(currentCommand, refreshedCommand, StringComparison.OrdinalIgnoreCase))
                note += " 应用当前 PATH 与系统登记 PATH 不同；新开终端或重启应用后再核对。";
            return new DeveloperToolSnapshot(installations, currentCommand, refreshedCommand, packageManagerPath, note);
        }

        public static string ResolvePathCommand(string path, string executable)
        {
            foreach (string directory in SplitPath(path))
            {
                try
                {
                    string candidate = Path.Combine(directory, executable);
                    if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }
            return "";
        }

        public static bool IsWindowsExecutionAlias(string path) => !string.IsNullOrEmpty(path)
            && path.Contains(@"\Microsoft\WindowsApps\", StringComparison.OrdinalIgnoreCase);

        private static IEnumerable<string> SplitPath(string path) => path.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')))
            .Where(Path.IsPathFullyQualified);

        private static void AddPathCandidates(string path, string executable, string source, Dictionary<string, string> candidates)
        {
            foreach (string directory in SplitPath(path))
            {
                try { AddCandidate(Path.Combine(directory, executable), source, candidates); }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }
        }

        private static void AddCandidate(string? path, string source, Dictionary<string, string> candidates)
        {
            if (!string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) && File.Exists(path))
                candidates.TryAdd(Path.GetFullPath(path), source);
        }

        private static void AddPythonRegistryCandidates(Dictionary<string, string> candidates)
        {
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? python = root.OpenSubKey(@"SOFTWARE\Python");
                    if (python == null) continue;
                    foreach (string companyName in python.GetSubKeyNames().Take(32))
                    {
                        using RegistryKey? company = python.OpenSubKey(companyName);
                        if (company == null) continue;
                        foreach (string tag in company.GetSubKeyNames().Take(64))
                        {
                            using RegistryKey? install = company.OpenSubKey(tag + @"\InstallPath");
                            if (install == null) continue;
                            string? path = install.GetValue("ExecutablePath") as string;
                            if (string.IsNullOrWhiteSpace(path) && install.GetValue("") is string directory)
                                path = Path.Combine(directory, "python.exe");
                            AddCandidate(path, $"注册表 · {companyName}/{tag}", candidates);
                        }
                    }
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }
        }

        private static void AddNodeRegistryCandidates(Dictionary<string, string> candidates)
        {
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using RegistryKey root = RegistryKey.OpenBaseKey(hive, view);
                    using RegistryKey? node = root.OpenSubKey(@"SOFTWARE\Node.js");
                    if (node?.GetValue("InstallPath") is string directory)
                        AddCandidate(Path.Combine(directory, "node.exe"), "注册表 · Node.js", candidates);
                }
                catch (Exception ex) when (IsInspectionFailure(ex)) { }
            }
        }

        private static string ReadPipVersion(string directory)
        {
            string sitePackages = Path.Combine(directory, "Lib", "site-packages");
            if (!Directory.Exists(sitePackages)) return "未检测到 pip";
            string? metadata = Directory.EnumerateDirectories(sitePackages, "pip-*.dist-info").Take(1).FirstOrDefault();
            if (metadata == null) return "未检测到 pip";
            return "pip " + Path.GetFileName(metadata)[4..^10];
        }

        private static string ReadNpmVersion(string directory)
        {
            string file = Path.Combine(directory, "node_modules", "npm", "package.json");
            if (!File.Exists(file) || new FileInfo(file).Length > 1024 * 1024) return "未检测到随附 npm";
            using JsonDocument json = JsonDocument.Parse(File.ReadAllText(file));
            return json.RootElement.TryGetProperty("version", out var version) && version.ValueKind == JsonValueKind.String ? "npm " + version.GetString() : "未知";
        }

        private static bool HasVersionManager() => new[] { "NVM_HOME", "FNM_DIR", "VOLTA_HOME" }
            .Any(name => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(name)));

        private static bool IsInspectionFailure(Exception ex) => ex is IOException or UnauthorizedAccessException
            or SecurityException or ArgumentException or NotSupportedException or JsonException or Win32Exception;
    }
}
