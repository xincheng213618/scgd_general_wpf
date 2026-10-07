using ColorVision.Database;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupFlowTemplateLoadingTests
{
    [Fact]
    public async Task SlowStorageReadLeavesDispatcherResponsiveAndPublishesOnUiThread()
    {
        string root = Directory.CreateTempSubdirectory("ColorVision-StartupFlow-").FullName;
        string fullRoot = Path.GetFullPath(root);
        string tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test cleanup path.");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        ObservableCollection<TemplateModel<FlowParam>>? previous = null;
        Dictionary<string, ITemplate>? previousRegistry = null;
        bool publishedOnUi = false;
        Task? loading = null;
        try
        {
            loading = WpfTestHost.Invoke(() =>
            {
                previous = TemplateFlow.Params;
                previousRegistry = TemplateControl.ITemplateNames;
                TemplateControl.ITemplateNames = new();
                TemplateFlow.Params = [new TemplateModel<FlowParam>("stale", new FlowParam { Id = -2, Name = "stale" })];
                TemplateFlow.Params.CollectionChanged += (_, _) => publishedOnUi = Application.Current.Dispatcher.CheckAccess();
                var storage = new LocalFlowTemplateStorage(new LocalTemplateStore(Path.Combine(root, "templates.db")));
                var template = new TemplateFlow(storage, () => true, () =>
                {
                    entered.TrySetResult();
                    if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException("Test storage was not released.");
                    throw new IOException("Synthetic unavailable database; use empty local storage.");
                });
                return template.LoadAsync();
            });
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            WpfTestHost.Invoke(() => Assert.Single(TemplateFlow.Params));
            Assert.False(loading.IsCompleted);
            release.Set();
            await loading.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(publishedOnUi);
            WpfTestHost.Invoke(() => Assert.Empty(TemplateFlow.Params));
        }
        finally
        {
            release.Set();
            if (loading != null) await loading;
            WpfTestHost.Invoke(() =>
            {
                if (previous != null) TemplateFlow.Params = previous;
                if (previousRegistry != null) TemplateControl.ITemplateNames = previousRegistry;
            });
            Directory.Delete(fullRoot, recursive: true);
        }
    }
}
