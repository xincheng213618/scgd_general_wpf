using ColorVision.ToolPlugins.ThirdPartyApps;
using ColorVision.UI;
using System.IO;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class SscomLauncherTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("ColorVision-Sscom-").FullName;
    private string BundledPath => Path.Combine(_root, SscomLauncher.FileName);
    private string BackupDirectory => Path.Combine(_root, "PackageCache", "Tools", "SSCOM");
    private string BackupPath => Path.Combine(BackupDirectory, SscomLauncher.FileName);
    private string RuntimeDirectory => Path.Combine(_root, "Tools", "SSCOM");
    private string RuntimePath => Path.Combine(RuntimeDirectory, SscomLauncher.FileName);

    [Fact]
    public void FirstLaunchCreatesTheBuiltInCommandListBeforeStartingSscom()
    {
        File.WriteAllText(BundledPath, "shipped-payload");
        var encoding = CodePagesEncodingProvider.Instance.GetEncoding(936)!;

        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, startInfo =>
        {
            string ini = File.ReadAllText(Path.Combine(startInfo.WorkingDirectory, "sscom51.ini"), encoding);
            Assert.Contains("N101=0,POWER_ON,0\r\nN1=H,50 47 2C 50 4F 57 45 52 2C 4F 4E 0A", ini);
            Assert.Contains("初始化测试 ProjectARVRInit", ini);
            Assert.Contains("N1063=,N\r\n", ini);
        });
    }

    [Fact]
    public void SelectingAPresetAddsItToAnExistingConfigurationWithoutReplacingCommands()
    {
        File.WriteAllText(BundledPath, "shipped-payload");
        Directory.CreateDirectory(RuntimeDirectory);
        string iniPath = Path.Combine(RuntimeDirectory, "sscom51.ini");
        File.WriteAllText(iniPath, "N101=1,Custom command,50\r\nN1=A,existing\r\nN102=0,,1000\r\nN2=A,\r\nN1081=,9600\r\n;end\r\n");
        SscomCommandPreset[] presets = [new("Read", [0x52, 0x0A])];
        bool launched = false;

        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, _ =>
        {
            launched = true;
            string ini = File.ReadAllText(iniPath);
            Assert.Contains("N101=1,Custom command,50\r\nN1=A,existing\r\n", ini);
            Assert.Contains("N102=0,Read,0\r\nN2=H,52 0A\r\n", ini);
            Assert.Contains("N1081=,9600\r\n", ini);
        }, presets);

        Assert.True(launched);
    }

    [Fact]
    public void ToolEntryUsesTheSameAppDataToolsDirectoryForLaunchingAndBrowsing()
    {
        var app = Assert.Single(new PortableAppProvider().GetThirdPartyApps(), app => app.Name == "SSCOM");
        string runtimeDirectory = Path.Combine(Environments.DirTools, "SSCOM");

        Assert.Equal(runtimeDirectory, app.InstallDirectory);
        Assert.Equal(Path.Combine(runtimeDirectory, SscomLauncher.FileName), app.PortableExePath);
        Assert.NotNull(app.LaunchAction);
        Assert.Single(app.ContextActions);
    }

    [Fact]
    public void RepairsBothExecutablesAndLaunchesOnlyTheRuntimeCopyWithoutTouchingSettings()
    {
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(RuntimeDirectory);
        File.WriteAllText(BundledPath, "shipped-payload");
        File.WriteAllText(BackupPath, "damaged-backup!");
        File.WriteAllText(RuntimePath, "damaged-runtime");
        Assert.Equal(new FileInfo(BundledPath).Length, new FileInfo(RuntimePath).Length);
        string iniPath = Path.Combine(RuntimeDirectory, "sscom51.ini");
        File.WriteAllText(iniPath, "saved settings");
        bool launched = false;

        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, startInfo =>
        {
            launched = true;
            Assert.Equal(RuntimePath, startInfo.FileName);
            Assert.Equal(RuntimeDirectory, startInfo.WorkingDirectory);
            Assert.Equal("shipped-payload", File.ReadAllText(startInfo.FileName));
            Assert.Throws<IOException>(() => File.OpenWrite(startInfo.FileName).Dispose());
        });

        Assert.True(launched);
        Assert.Equal("shipped-payload", File.ReadAllText(BackupPath));
        Assert.Equal("saved settings", File.ReadAllText(iniPath));
    }

    [Fact]
    public void RestoresFromTheVerifiedBackupWhenTheBundledExecutableAndRuntimeAreMissing()
    {
        File.WriteAllText(BundledPath, "shipped-payload");
        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, _ => { });
        File.Delete(BundledPath);
        File.Delete(RuntimePath);

        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, startInfo =>
        {
            Assert.Equal(RuntimePath, startInfo.FileName);
            Assert.Equal("shipped-payload", File.ReadAllText(startInfo.FileName));
        });
    }

    [Fact]
    public void RefusesToStartWithoutTheBundledExecutableOrAMatchingBackup()
    {
        File.WriteAllText(BundledPath, "shipped-payload");
        SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory, _ => { });
        File.Delete(BundledPath);
        File.WriteAllText(BackupPath, "damaged-backup!");

        Assert.Throws<InvalidDataException>(() => SscomLauncher.Launch(BundledPath, BackupDirectory, RuntimeDirectory,
            _ => throw new InvalidOperationException("SSCOM was unexpectedly launched.")));
        Assert.Equal("shipped-payload", File.ReadAllText(RuntimePath));
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
