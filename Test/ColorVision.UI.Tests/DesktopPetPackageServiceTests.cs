using ColorVision.FloatingBall;
using SkiaSharp;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests
{
    public sealed class DesktopPetPackageServiceTests : IDisposable
    {
        private readonly string _tempDirectory = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            $"ColorVisionDesktopPetPackageTests-{Guid.NewGuid():N}"));
        private readonly string _packageDirectory;

        public DesktopPetPackageServiceTests()
        {
            _packageDirectory = Path.Combine(_tempDirectory, "packages");
            Directory.CreateDirectory(_tempDirectory);
        }

        [Fact]
        public async Task ImportCreatesDecodablePackageWithCamelCaseManifest()
        {
            var sourcePath = CreateSpriteSheet("source.png", frameWidth: 3, frameHeight: 4, rowCount: 11);

            var result = await DesktopPetPackageService.ImportAsync(
                new DesktopPetImportRequest(
                    "Test Pet",
                    "A test companion.",
                    sourcePath,
                    SpriteVersionNumber: 2),
                _packageDirectory);

            var expectedPackageDirectory = Path.Combine(_packageDirectory, "test-pet");
            Assert.Equal("colorvision-custom:test-pet", result.AssetId);
            Assert.Equal(expectedPackageDirectory, result.PackageDirectory);
            Assert.True(File.Exists(Path.Combine(expectedPackageDirectory, "spritesheet.png")));

            using var manifest = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(expectedPackageDirectory, "pet.json")));
            Assert.Equal("test-pet", manifest.RootElement.GetProperty("id").GetString());
            Assert.Equal("Test Pet", manifest.RootElement.GetProperty("displayName").GetString());
            Assert.Equal("A test companion.", manifest.RootElement.GetProperty("description").GetString());
            Assert.Equal(2, manifest.RootElement.GetProperty("spriteVersionNumber").GetInt32());
            Assert.Equal("spritesheet.png", manifest.RootElement.GetProperty("spritesheetPath").GetString());
            Assert.False(manifest.RootElement.TryGetProperty("DisplayName", out _));

            using var spriteSheet = DesktopPetSpriteSheet.Load(
                File.ReadAllBytes(Path.Combine(expectedPackageDirectory, "spritesheet.png")),
                spriteVersionNumber: 2);
            Assert.Equal(11, spriteSheet.RowCount);
            Assert.Equal(3, spriteSheet.FrameWidth);
            Assert.Equal(4, spriteSheet.FrameHeight);
        }

        [Fact]
        public async Task RepeatedNameCreatesANewPackageWithoutOverwritingTheFirst()
        {
            var firstSourcePath = CreateSpriteSheet("first.png", 2, 2, 11, SKColors.CornflowerBlue);
            var secondSourcePath = CreateSpriteSheet("second.png", 2, 2, 11, SKColors.Orange);

            var first = await DesktopPetPackageService.ImportAsync(
                new DesktopPetImportRequest("Repeat Pet", "First", firstSourcePath, 2),
                _packageDirectory);
            var firstBytes = File.ReadAllBytes(Path.Combine(first.PackageDirectory, "spritesheet.png"));

            var second = await DesktopPetPackageService.ImportAsync(
                new DesktopPetImportRequest("Repeat Pet", "Second", secondSourcePath, 2),
                _packageDirectory);

            Assert.Equal("colorvision-custom:repeat-pet", first.AssetId);
            Assert.Equal("colorvision-custom:repeat-pet-2", second.AssetId);
            Assert.Equal(
                firstBytes,
                File.ReadAllBytes(Path.Combine(first.PackageDirectory, "spritesheet.png")));
            Assert.NotEqual(
                firstBytes,
                File.ReadAllBytes(Path.Combine(second.PackageDirectory, "spritesheet.png")));
        }

        [Fact]
        public async Task InvalidSpriteGridLeavesNoPackageOrStagingDirectory()
        {
            var invalidSourcePath = CreateImage("invalid.png", width: 10, height: 22, SKColors.Red);

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                DesktopPetPackageService.ImportAsync(
                    new DesktopPetImportRequest("Invalid Pet", string.Empty, invalidSourcePath, 2),
                    _packageDirectory));

            Assert.False(Directory.Exists(_packageDirectory));
        }

        [Fact]
        public void StagingCleanupOnlyDeletesContainedImportDirectories()
        {
            Directory.CreateDirectory(_packageDirectory);
            var stagingDirectory = Path.Combine(_packageDirectory, ".import-test");
            var normalDirectory = Path.Combine(_packageDirectory, "keep-me");
            Directory.CreateDirectory(stagingDirectory);
            Directory.CreateDirectory(normalDirectory);
            File.WriteAllText(Path.Combine(stagingDirectory, "partial.tmp"), "partial");
            File.WriteAllText(Path.Combine(normalDirectory, "pet.json"), "{}");

            DesktopPetPackageService.DeleteStagingDirectory(_packageDirectory, stagingDirectory);
            DesktopPetPackageService.DeleteStagingDirectory(_packageDirectory, normalDirectory);

            Assert.False(Directory.Exists(stagingDirectory));
            Assert.True(Directory.Exists(normalDirectory));
        }

        public void Dispose()
        {
            var expectedPrefix = Path.GetFullPath(Path.GetTempPath())
                .TrimEnd(Path.DirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            if (_tempDirectory.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(_tempDirectory).StartsWith(
                    "ColorVisionDesktopPetPackageTests-",
                    StringComparison.Ordinal)
                && Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
        }

        private string CreateSpriteSheet(
            string fileName,
            int frameWidth,
            int frameHeight,
            int rowCount,
            SKColor? color = null)
        {
            return CreateImage(
                fileName,
                checked(frameWidth * DesktopPetSpriteSheet.ColumnCount),
                checked(frameHeight * rowCount),
                color ?? SKColors.CornflowerBlue);
        }

        private string CreateImage(string fileName, int width, int height, SKColor color)
        {
            var path = Path.Combine(_tempDirectory, fileName);
            using var bitmap = new SKBitmap(width, height);
            bitmap.Erase(color);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, quality: 100);
            File.WriteAllBytes(path, data.ToArray());
            return path;
        }
    }
}
