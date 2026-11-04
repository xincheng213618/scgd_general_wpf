using ColorVision.Engine.Templates.Jsons.AAFindPoints;
using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace ProjectARVRPro.AlgorithmEvidence;

internal sealed record AaEvidenceParameters(string Name, string Json);

internal static class ServerAaEvidenceSettings
{
    internal static IReadOnlyList<AaEvidenceParameters> ReadParameters()
    {
        // Use the provider-maintained schema default, explicitly labelled as a reference.
        var assembly = typeof(TemplateAAFindPoints).Assembly;
        string resource = assembly.GetManifestResourceNames().First(name => name.Replace('\\', '/') == "Templates/Jsons/AAFindPoints/FindLightArea.schema.json");
        using Stream schema = assembly.GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException("AA 参数定义缺失，请修复主程序安装。");
        using JsonDocument document = JsonDocument.Parse(schema);
        List<AaEvidenceParameters> choices = [new("AA 参考参数（非历史参数）", document.RootElement.GetProperty("default").GetRawText())];
        choices.AddRange(TemplateAAFindPoints.Params.Select(template => new AaEvidenceParameters("当前模板 · " + template.Key, template.Value.JsonValue)));
        return choices;
    }

    internal static string FindLibrary()
    {
        string[] localCandidates =
        [
            Path.Combine(AppContext.BaseDirectory, "ServerAlgorithms", "AA", "CV_algorithm.dll"),
            Path.Combine(AppContext.BaseDirectory, "CV_algorithm.dll")
        ];
        foreach (string path in localCandidates) if (File.Exists(path)) return path;
        // Discover the installation from SCM registration; no machine-specific desktop paths.
        foreach (string service in new[] { "CVMainService_x64", "CVMainService_dev" })
        {
            try
            {
                using RegistryKey? key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + service);
                if (key?.GetValue("ImagePath") is not string command) continue;
                string? executable = ReadExecutablePath(Environment.ExpandEnvironmentVariables(command));
                if (executable == null || Path.GetDirectoryName(executable) is not string directory) continue;
                string path = Path.Combine(directory, "plugin", "Algorithm", "CV_algorithm.dll");
                if (File.Exists(path)) return path;
            }
            catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or IOException) { }
        }
        return string.Empty;
    }

    internal static string? ReadExecutablePath(string command)
    {
        command = command.Trim();
        if (command.StartsWith('"'))
        {
            int endQuote = command.IndexOf('"', 1);
            return endQuote > 1 ? command[1..endQuote] : null;
        }
        int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return end >= 0 ? command[..(end + 4)] : null;
    }
}
