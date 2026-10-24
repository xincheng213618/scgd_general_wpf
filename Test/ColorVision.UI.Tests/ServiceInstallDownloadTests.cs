using ColorVision.UI;
using System.IO;
using System.Net.Http;
using System.Reflection;
using WindowsServicePlugin.ServiceManager;

namespace ColorVision.UI.Tests;

public sealed class ServiceInstallDownloadTests
{
    private static MethodInfo Method(string name) => typeof(ServiceInstallViewModel).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    [Theory]
    [InlineData("/api/tool/cvwindowsservice/download/4.2.1.813")]
    [InlineData("/download/Tool/CVWindowsService/CVWindowsService[4.2.1.813]-0813.zip")]
    public async Task ReleaseFileNameReachesDownloadServiceEvenWhenUrlEndsInVersion(string downloadUrl)
    {
        string releases = $$"""
            {"latestVersion":"4.2.1.813","packages":[
              {"version":"4.2.1.813","suffix":"0813","fileName":"CVWindowsService[4.2.1.813]-0813.zip","downloadUrl":"{{downloadUrl}}"}
            ]}
            """;
        object?[] arguments = [releases, new Uri("http://example.invalid"), null];
        Assert.True((bool)Method("TryParseCvwsReleases").Invoke(null, arguments)!);
        object package = arguments[2]!;
        string fileName = (string)package.GetType().GetProperty("FileName")!.GetValue(package)!;
        string url = (string)package.GetType().GetProperty("DownloadUrl")!.GetValue(package)!;
        RecordingDownloader downloader = new();

        var download = (Task<string?>)Method("DownloadWithServiceAsync").Invoke(null, [downloader, url, @"C:\Downloads", fileName])!;

        Assert.Equal("CVWindowsService[4.2.1.813]-0813.zip", downloader.FileName);
        Assert.Equal(new Uri(new Uri("http://example.invalid"), downloadUrl).ToString(), downloader.Url);
        Assert.Equal(Path.Combine(@"C:\Downloads", fileName), await download);
    }

    [Theory]
    [InlineData("""{"latestVersion":"4.2.1.813","packages":[]}""", "CVWindowsService[4.2.1.813].zip")]
    [InlineData("""{"latestVersion":"4.2.1.813","packages":[{"version":"4.2.1.813","downloadUrl":"/download/CVWindowsService[4.2.1.813]-0813.zip"}]}""", "CVWindowsService[4.2.1.813]-0813.zip")]
    public void MissingFileNameUsesZipUrlOrVersionedZipFallback(string releases, string expected)
    {
        object?[] arguments = [releases, new Uri("http://example.invalid"), null];

        Assert.True((bool)Method("TryParseCvwsReleases").Invoke(null, arguments)!);

        object package = arguments[2]!;
        Assert.Equal(expected, package.GetType().GetProperty("FileName")!.GetValue(package));
    }

    [Fact]
    public async Task FailedComponentReportsItsNameWhileSuccessfulComponentStillApplies()
    {
        TaskCompletionSource<string> completedPackage = new(TaskCreationOptions.RunContinuationsAsynchronously);
        string? appliedPath = null;
        Func<Task<string>> downloadService = () => completedPackage.Task;
        Action<string> applyService = path => appliedPath = path;
        Func<Task<string>> downloadMySql = () => Task.FromException<string>(new HttpRequestException("Request failed", new IOException("Connection reset")));
        Action<string> applyMySql = _ => Assert.Fail("A failed download must not be applied.");
        Task<string?> service = (Task<string?>)Method("DownloadAndApplyComponentAsync").Invoke(null, ["CVWindowsService", downloadService, applyService])!;
        Task<string?> mysql = (Task<string?>)Method("DownloadAndApplyComponentAsync").Invoke(null, ["MySQL", downloadMySql, applyMySql])!;

        Assert.Equal("MySQL: Connection reset", await mysql);
        completedPackage.SetResult(@"C:\Downloads\CVWindowsService[4.2.1.813]-0813.zip");

        Assert.Null(await service);
        Assert.Equal(@"C:\Downloads\CVWindowsService[4.2.1.813]-0813.zip", appliedPath);
    }

    private sealed class RecordingDownloader : IDownloadService
    {
        public string? FileName { get; private set; }
        public string? Url { get; private set; }
        public void Download(string url, string saveDir, string? authorization = null, Action<string?>? onCompleted = null)
            => Assert.Fail("Service packages must supply an explicit file name.");
        public void Download(string url, string saveDir, string? authorization, Action<string?>? onCompleted, string fileName)
        {
            FileName = fileName;
            Url = url;
            onCompleted?.Invoke(Path.Combine(saveDir, fileName));
        }
        public void ShowDownloadWindow() { }
        public void CloseDownloadWindow() { }
    }
}
