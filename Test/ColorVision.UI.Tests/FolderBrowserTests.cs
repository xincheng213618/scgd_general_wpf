using ColorVision.Solution.Editor;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.Tests;

public sealed class FolderBrowserTests
{
    [Fact]
    public async Task FolderBrowserLoadsCurrentEntriesAfterTheTabIsCreated()
    {
        DirectoryInfo folder = Directory.CreateTempSubdirectory("ColorVision-FolderBrowser-");
        string subfolder = Path.Combine(folder.FullName, "z-folder");
        string visibleFile = Path.Combine(folder.FullName, "a-visible.txt");
        string hiddenFile = Path.Combine(folder.FullName, "hidden.txt");
        try
        {
            await WpfTestHost.Invoke<Task>(async () =>
            {
                var browser = new FolderBrowserControl(folder.FullName);
                var list = Assert.Single(Assert.IsType<DockPanel>(browser.Content).Children.OfType<ListView>());
                Assert.Null(list.ItemsSource);

                Directory.CreateDirectory(subfolder);
                File.WriteAllText(visibleFile, "visible");
                File.WriteAllText(hiddenFile, "hidden");
                File.SetAttributes(hiddenFile, FileAttributes.Hidden);
                var populated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var itemsSource = DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListView));
                void OnItemsSourceChanged(object? sender, EventArgs args) => populated.TrySetResult();
                itemsSource.AddValueChanged(list, OnItemsSourceChanged);
                try
                {
                    browser.RaiseEvent(new RoutedEventArgs(FrameworkElement.LoadedEvent));
                    await populated.Task.WaitAsync(TimeSpan.FromSeconds(10));

                    Assert.Equal(new[] { subfolder, visibleFile }, list.Items.Cast<FolderBrowserEntry>().Select(entry => entry.FullPath));
                }
                finally
                {
                    browser.RaiseEvent(new RoutedEventArgs(FrameworkElement.UnloadedEvent));
                    itemsSource.RemoveValueChanged(list, OnItemsSourceChanged);
                }
            });
        }
        finally
        {
            File.Delete(visibleFile);
            File.Delete(hiddenFile);
            if (Directory.Exists(subfolder)) Directory.Delete(subfolder);
            folder.Delete();
        }
    }
}
