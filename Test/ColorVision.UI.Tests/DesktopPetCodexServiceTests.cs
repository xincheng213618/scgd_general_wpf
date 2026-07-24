using ColorVision.FloatingBall;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests
{
    public sealed class DesktopPetCodexServiceTests : IDisposable
    {
        private readonly string _tempDirectory = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionDesktopPetCodexTests-{Guid.NewGuid():N}"));

        public DesktopPetCodexServiceTests()
        {
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public void BuildPromptUsesHatchPetAndNormalizesTheConcept()
        {
            var prompt = DesktopPetCodexService.BuildPrompt("  一只蓝色的猫\n  安静但可靠  ");

            Assert.StartsWith("$hatch-pet ", prompt, StringComparison.Ordinal);
            Assert.Contains("一只蓝色的猫 安静但可靠", prompt, StringComparison.Ordinal);
            Assert.Contains("default Codex pets directory", prompt, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildPromptFallsBackToCodexPersonalContext()
        {
            var prompt = DesktopPetCodexService.BuildPrompt(" ");

            Assert.Contains("based on what you know about me", prompt, StringComparison.Ordinal);
        }

        [Fact]
        public void BuildPromptRejectsConceptBeyondTheUiLimit()
        {
            var concept = new string('猫', DesktopPetCodexService.MaximumConceptLength + 1);

            var exception = Assert.Throws<ArgumentException>(
                () => DesktopPetCodexService.BuildPrompt(concept));

            Assert.Contains(
                DesktopPetCodexService.MaximumConceptLength.ToString(),
                exception.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public void NewThreadDeepLinkRoundTripsUnicodePrompt()
        {
            const string prompt = "$hatch-pet 创建一只蓝色猫。";

            var deepLink = DesktopPetCodexService.BuildNewThreadDeepLink(prompt);
            var uri = new Uri(deepLink);

            Assert.Equal("codex", uri.Scheme);
            Assert.Equal("new", uri.Host);
            Assert.StartsWith("?prompt=", uri.Query, StringComparison.Ordinal);
            Assert.Equal(prompt, Uri.UnescapeDataString(uri.Query["?prompt=".Length..]));
        }

        [Fact]
        public async Task SkillInstallCopiesTheBundledTreeAndPreservesAnExistingInstall()
        {
            var sourceDirectory = Path.Combine(_tempDirectory, "bundled", "hatch-pet");
            var nestedDirectory = Path.Combine(sourceDirectory, "references");
            Directory.CreateDirectory(nestedDirectory);
            File.WriteAllText(Path.Combine(sourceDirectory, "SKILL.md"), "bundled-v1");
            File.WriteAllText(Path.Combine(nestedDirectory, "contract.md"), "contract");
            var destinationDirectory = Path.Combine(_tempDirectory, "codex", "skills", "hatch-pet");

            var installedDirectory = await DesktopPetCodexService.EnsureHatchPetInstalledAsync(
                sourceDirectory,
                destinationDirectory);

            Assert.Equal(destinationDirectory, installedDirectory);
            Assert.Equal("bundled-v1", File.ReadAllText(Path.Combine(destinationDirectory, "SKILL.md")));
            Assert.Equal("contract", File.ReadAllText(Path.Combine(destinationDirectory, "references", "contract.md")));
            Assert.Empty(Directory.GetDirectories(
                Path.GetDirectoryName(destinationDirectory)!,
                $".{DesktopPetCodexService.SkillName}-install-*"));

            File.WriteAllText(Path.Combine(destinationDirectory, "SKILL.md"), "user-customized");
            File.WriteAllText(Path.Combine(sourceDirectory, "SKILL.md"), "bundled-v2");
            await DesktopPetCodexService.EnsureHatchPetInstalledAsync(sourceDirectory, destinationDirectory);

            Assert.Equal("user-customized", File.ReadAllText(Path.Combine(destinationDirectory, "SKILL.md")));
        }

        [Fact]
        public async Task SkillInstallRejectsAnIncompleteExistingDirectoryWithoutDeletingIt()
        {
            var sourceDirectory = Path.Combine(_tempDirectory, "bundled", "hatch-pet");
            Directory.CreateDirectory(sourceDirectory);
            File.WriteAllText(Path.Combine(sourceDirectory, "SKILL.md"), "bundled");
            var destinationDirectory = Path.Combine(_tempDirectory, "codex", "skills", "hatch-pet");
            Directory.CreateDirectory(destinationDirectory);
            var markerPath = Path.Combine(destinationDirectory, "keep-me.txt");
            File.WriteAllText(markerPath, "user data");

            var exception = await Assert.ThrowsAsync<InvalidDataException>(
                () => DesktopPetCodexService.EnsureHatchPetInstalledAsync(
                    sourceDirectory,
                    destinationDirectory));

            Assert.Contains("缺少 SKILL.md", exception.Message, StringComparison.Ordinal);
            Assert.Equal("user data", File.ReadAllText(markerPath));
        }

        [Fact]
        public void PetPackageSnapshotOnlyIncludesCompletedManifests()
        {
            var petDirectory = Path.Combine(_tempDirectory, "pets");
            var completedDirectory = Path.Combine(petDirectory, "blue-cat");
            var incompleteDirectory = Path.Combine(petDirectory, "still-rendering");
            Directory.CreateDirectory(completedDirectory);
            Directory.CreateDirectory(incompleteDirectory);
            File.WriteAllText(Path.Combine(completedDirectory, "pet.json"), "{}");

            var ids = DesktopPetCodexService.SnapshotPetPackageIds(petDirectory);

            Assert.Contains("codex-custom:pets:blue-cat", ids);
            Assert.DoesNotContain("codex-custom:pets:still-rendering", ids);
        }

        public void Dispose()
        {
            var expectedPrefix = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (_tempDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(_tempDirectory).StartsWith(
                    "ColorVisionDesktopPetCodexTests-",
                    StringComparison.Ordinal)
                && Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }
    }
}
