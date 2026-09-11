using ColorVision.Engine.Media;
using ColorVision.Engine.Services.PhyCameras.Calibration;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class LumFourColorReplacementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReplacementBacksUpExactBytesPreservesMetadataAndOnlyThenRestarts(bool multiColor)
    {
        using var files = new CalibrationFiles(multiColor);
        byte[] original = File.ReadAllBytes(files.Path);
        var source = LumFourColorSourceSnapshot.Load(files.Path);
        var corrected = LumFourColorCorrectionCalculator.CorrectSinglePoint(source.Config, new(new(2, .2, .3), new(4, .2, .3)));
        int restarts = 0;
        var result = await LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(source, corrected, () =>
        {
            restarts++;
            Assert.Equal(original, File.ReadAllBytes(Assert.Single(Directory.GetFiles(files.Directory, "*_backup.dat"))));
            Assert.Equal(corrected.A, LumFourColorSourceSnapshot.Load(files.Path).Config.A);
            return Task.CompletedTask;
        });
        Assert.Null(result.RestartError);
        Assert.Equal(1, restarts);
        Assert.Equal(files.Directory, Path.GetDirectoryName(result.BackupPath));
        Assert.Matches(@"source_\d{8}_\d{6}_\d{3}_.*_backup\.dat$", result.BackupPath);
        Assert.Equal(original, File.ReadAllBytes(result.BackupPath));
        JObject saved = JObject.Parse(File.ReadAllText(files.Path));
        JObject before = JObject.Parse(System.Text.Encoding.UTF8.GetString(original));
        foreach (var property in before.Properties().Where(p => multiColor ? p.Name != "pa" : !"abcdefghi".Contains(p.Name, StringComparison.Ordinal)))
            Assert.True(JToken.DeepEquals(property.Value, saved[property.Name]), property.Name);
        Assert.Equal(multiColor, saved.ContainsKey("pa"));
        Assert.Throws<InvalidOperationException>(source.EnsureUnchanged);
        var second = LumFourColorSourceSnapshot.Load(files.Path).ReplaceOriginal(source.Config);
        Assert.NotEqual(result.BackupPath, second);
        Assert.Equal(corrected.A, LumFourColorSourceSnapshot.Load(second).Config.A);
        Assert.Equal(original, File.ReadAllBytes(result.BackupPath));
        Assert.Empty(Directory.GetFiles(files.Directory, "*.tmp"));
    }

    [Theory]
    [InlineData("changed")]
    [InlineData("invalid")]
    [InlineData("locked")]
    public async Task InvalidOrUnwritableReplacementNeverRestartsOrChangesOriginal(string failure)
    {
        using var files = new CalibrationFiles(false);
        var source = LumFourColorSourceSnapshot.Load(files.Path);
        var corrected = new CVRawManualCieConfig { A = failure == "invalid" ? double.NaN : 2, E = 2, I = 2 };
        if (failure == "changed") File.AppendAllText(files.Path, " ");
        byte[] original = File.ReadAllBytes(files.Path);
        using var fileLock = failure == "locked" ? File.Open(files.Path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
        int restarts = 0;
        await Assert.ThrowsAnyAsync<Exception>(() => LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(source, corrected,
            () => { restarts++; return Task.CompletedTask; }));
        Assert.Equal(0, restarts);
        Assert.Equal(original, File.ReadAllBytes(files.Path));
        Assert.Empty(Directory.GetFiles(files.Directory, "*.tmp"));
        if (failure != "locked") Assert.Empty(Directory.GetFiles(files.Directory, "*_backup.dat"));
    }

    [Fact]
    public async Task RestartFailureKeepsNewFileAndRecoverableBackupAndReleasesGate()
    {
        using var files = new CalibrationFiles(false);
        byte[] original = File.ReadAllBytes(files.Path);
        var source = LumFourColorSourceSnapshot.Load(files.Path);
        var corrected = new CVRawManualCieConfig { A = 2, E = 2, I = 2 };
        var failure = new IOException("test service unavailable");
        var result = await LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(source, corrected,
            () => Task.FromException(failure));
        Assert.Same(failure, result.RestartError);
        Assert.Equal(2, LumFourColorSourceSnapshot.Load(files.Path).Config.A);
        Assert.Equal(original, File.ReadAllBytes(result.BackupPath));
        Assert.Contains("文件已替换", result.Message);
        Assert.Contains("服务重启失败", result.Message);
        var retry = await LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(LumFourColorSourceSnapshot.Load(files.Path), source.Config,
            () => Task.CompletedTask);
        Assert.Null(retry.RestartError);
    }

    [Fact]
    public async Task PendingRestartRejectsAnotherReplacementWithoutTouchingItsFile()
    {
        using var first = new CalibrationFiles(false);
        using var second = new CalibrationFiles(true);
        var source = LumFourColorSourceSnapshot.Load(first.Path);
        var other = LumFourColorSourceSnapshot.Load(second.Path);
        var pending = new TaskCompletionSource();
        Task<LumFourColorReplacementResult> operation = LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(source, source.Config,
            () => pending.Task);
        try
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => LumFourColorCalibrationReplacement.ReplaceAndRestartAsync(other, other.Config,
                () => throw new Exception("must not restart")));
            other.EnsureUnchanged();
            Assert.Single(Directory.GetFiles(second.Directory));
        }
        finally { pending.SetResult(); await operation; }
    }

    private sealed class CalibrationFiles : IDisposable
    {
        public string Directory { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lum-replacement-" + Guid.NewGuid().ToString("N"));
        public string Path => System.IO.Path.Combine(Directory, "source.dat");
        public CalibrationFiles(bool multiColor)
        {
            System.IO.Directory.CreateDirectory(Directory);
            JObject source = multiColor
                ? new JObject { ["Gain"] = new JArray(1, 2, 3, 4), ["pa"] = new JArray(1, 0, 0, 0, 1, 0, 0, 0, 1) }
                : JObject.Parse(LumFourColorCorrectionCalculator.SerializeCalibrationFile(new() { Gain_x = 1, Gain_y = 2, Gain_z = 3, A = 1, E = 1, I = 1 }));
            source["device"] = "test-only";
            source["bpp"] = 16;
            File.WriteAllText(Path, source.ToString());
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, recursive: true);
    }
}
