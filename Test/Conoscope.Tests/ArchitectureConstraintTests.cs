using System.Text.Json;
using System.Text.RegularExpressions;
using System.IO;

namespace Conoscope.Tests
{
    public class ArchitectureConstraintTests
    {
        private static string RepoRoot => FindRepoRoot();

        private static string FindRepoRoot()
        {
            DirectoryInfo? current = new(AppContext.BaseDirectory);
            while (current != null)
            {
                string candidate = Path.Combine(current.FullName, "Plugins", "Conoscope", "Conoscope.csproj");
                if (File.Exists(candidate))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        }

        [Fact]
        public void VersionPrefix_MatchesManifestJson()
        {
            string csprojPath = Path.Combine(RepoRoot, "Plugins", "Conoscope", "Conoscope.csproj");
            string manifestPath = Path.Combine(RepoRoot, "Plugins", "Conoscope", "manifest.json");

            Assert.True(File.Exists(csprojPath), $"csproj not found: {csprojPath}");
            Assert.True(File.Exists(manifestPath), $"manifest.json not found: {manifestPath}");

            string csprojContent = File.ReadAllText(csprojPath);
            Match versionMatch = Regex.Match(csprojContent, @"<VersionPrefix>([^<]+)</VersionPrefix>");
            Assert.True(versionMatch.Success, "VersionPrefix not found in csproj");
            string csprojVersion = versionMatch.Groups[1].Value;

            string manifestContent = File.ReadAllText(manifestPath);
            using JsonDocument doc = JsonDocument.Parse(manifestContent);
            string manifestVersion = doc.RootElement.GetProperty("version").GetString() ?? "";

            Assert.Equal(csprojVersion, manifestVersion);
        }

        [Fact]
        public void ApplicationLayer_DoesNotContainMessageBoxShow()
        {
            string appDir = Path.Combine(RepoRoot, "Plugins", "Conoscope", "Application");
            if (!Directory.Exists(appDir))
            {
                return;
            }

            string[] csFiles = Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories);
            foreach (string file in csFiles)
            {
                string content = File.ReadAllText(file);
                Assert.False(
                    content.Contains("MessageBox.Show"),
                    $"Application layer file should not use MessageBox.Show: {Path.GetRelativePath(RepoRoot, file)}");
            }
        }
    }
}
