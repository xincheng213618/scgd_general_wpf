using ColorVision.Recovery;
using ColorVision.UI.Serach;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupMaintenanceSearchHostTests
{
    [Theory]
    [InlineData("向导", "maintenance:setup-wizard", "初始化向导")]
    [InlineData("恢复", "maintenance:startup-recovery", "故障恢复")]
    public void RealSearchControlDisplaysLocalMaintenanceWithoutRequestingIt(string query, string id, string title)
    {
        WpfTestHost.Invoke(() =>
        {
            CultureInfo previous = CultureInfo.CurrentUICulture;
            ResourceDictionary resources = Application.Current.Resources;
            var locals = resources.Keys.Cast<object>().ToDictionary(key => key, key => resources[key]);
            var dictionaries = resources.MergedDictionaries.ToArray();
            SearchControl? control = null;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                resources.Clear();
                resources.MergedDictionaries.Clear();
                var requests = new List<StartupMaintenanceMode>();
                var recent = new List<string>();
                var provider = new StartupMaintenanceSearchProvider(requests.Add);
                var config = new SearchConfig { EnableBrowserSearch = false, EnableEverythingSearch = false };
                var manager = new SearchManager(() => [typeof(StartupMaintenanceSearchProvider).Assembly],
                    _ => [typeof(StartupMaintenanceSearchProvider)], () => config, () => [], _ => provider);
                control = new SearchControl((text, category, token) => manager.QueryAsync(text, token, category: category),
                    recent.Add, manager.InvalidateCatalog);
                foreach (string source in new[] { "/HandyControl;component/Themes/basic/colors/colors.xaml",
                    "/HandyControl;component/Themes/Theme.xaml", "/ColorVision.Themes;component/Themes/White.xaml",
                    "/ColorVision.Themes;component/Themes/Base.xaml" })
                    resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(source, UriKind.Relative) });
                var root = new Grid { Width = 720, Height = 420 };
                root.Children.Add(control);
                control.Open(null);
                Complete(control.Model.PendingSearch);
                // A completed provider query does not mean WPF has attached the
                // off-screen tree's inherited bindings. Settle layout/data binding before typing.
                root.Measure(new Size(720, 420));
                root.Arrange(new Rect(0, 0, 720, 420));
                root.UpdateLayout();
                root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var input = Assert.IsType<TextBox>(control.FindName("Searchbox"));
                var binding = input.GetBindingExpression(TextBox.TextProperty)!;
                Assert.Same(control.Model, input.DataContext);
                Assert.Equal(System.Windows.Data.BindingStatus.Active, binding.Status);
                input.SetCurrentValue(TextBox.TextProperty, query);
                binding.UpdateSource();
                root.Dispatcher.Invoke(() => { }, DispatcherPriority.DataBind);
                Assert.Equal(query, control.Model.SearchText);
                Complete(control.Model.PendingSearch);
                for (int pass = 0; pass < 2; pass++)
                {
                    root.Measure(new Size(720, 420));
                    root.Arrange(new Rect(0, 0, 720, 420));
                    root.UpdateLayout();
                    root.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                }
                SearchPaletteEntry entry = Assert.Single(control.Model.Results);
                Assert.Equal(id, entry.Result.Source.GuidId);
                Assert.Equal(title, entry.Title);
                Assert.Equal("高级维护", entry.Category);
                Assert.True(entry.IsAvailable);
                Assert.Empty(entry.ShortcutText);
                var list = Assert.IsType<ListBox>(control.FindName("ListViewSearch"));
                Assert.Same(entry, Assert.Single(list.Items.Cast<SearchPaletteEntry>()));
                var row = Assert.IsType<ListBoxItem>(list.ItemContainerGenerator.ContainerFromIndex(0));
                Assert.True(row.ActualWidth > 0 && row.ActualHeight > 0);
                Assert.Empty(requests);
                Assert.Empty(recent);
            }
            finally
            {
                control?.Close();
                resources.Clear();
                resources.MergedDictionaries.Clear();
                foreach (ResourceDictionary dictionary in dictionaries) resources.MergedDictionaries.Add(dictionary);
                foreach ((object key, object value) in locals) resources[key] = value;
                CultureInfo.CurrentUICulture = previous;
            }
        });
    }

    private static void Complete(Task task)
    {
        if (!task.IsCompleted)
        {
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
            timer.Tick += (_, _) => frame.Continue = false;
            Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
            _ = task.ContinueWith(_ => dispatcher.BeginInvoke(DispatcherPriority.Send, () => frame.Continue = false), TaskScheduler.Default);
            timer.Start();
            try { Dispatcher.PushFrame(frame); }
            finally { timer.Stop(); }
        }
        Assert.True(task.IsCompleted, "The isolated maintenance search did not complete within five seconds.");
        task.GetAwaiter().GetResult();
    }
}
