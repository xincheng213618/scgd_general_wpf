using ColorVision.Solution;
using ColorVision.Solution.Explorer;
using ColorVision.Themes;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class SolutionTreeRevealTests
{
    [Fact]
    public void RevealScrollsToAnUnrealizedFileInsideAnUnrealizedFolder()
    {
        WpfTestHost.Invoke(() =>
        {
            var managerField = typeof(SolutionManager).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
            object? previousManager = managerField.GetValue(null);
            var dictionaries = Application.Current.Resources.MergedDictionaries;
            var theme = ThemeManager.ResourceDictionaryWhite.Concat(ThemeManager.ResourceDictionaryBase)
                .Select(uri => new ResourceDictionary { Source = new Uri(uri, UriKind.Relative) }).ToArray();
            try
            {
                // Isolate the view from startup workspace restore and the user's recent paths.
                managerField.SetValue(null, RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager)));
                foreach (ResourceDictionary dictionary in theme) dictionaries.Add(dictionary);
                using var source = new HwndSource(new HwndSourceParameters("Explorer reveal test", 380, 520));
                var control = new TreeViewControl();
                source.RootVisual = control;
                Layout(control);

                var tree = (TreeView)control.FindName("SolutionTreeView");
                var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
                var root = new SolutionFolderNode(explorer, new() { Id = "root", Name = "Root" }) { IsExpanded = true };
                AddFiles(root, 160);
                var folder = new SolutionFolderNode(explorer, new() { Id = "nested", Name = "Nested" }) { Parent = root };
                root.VisualChildren.Add(folder);
                AddFiles(folder, 70);
                SolutionNode target = folder.VisualChildren[^1];

                tree.ItemsSource = new[] { root };
                Layout(control);
                var rootContainer = Assert.IsType<TreeViewItem>(tree.ItemContainerGenerator.ContainerFromItem(root));
                Assert.Null(rootContainer.ItemContainerGenerator.ContainerFromItem(folder));

                control.BringNodeIntoView(target);
                Layout(control);

                var folderContainer = Assert.IsType<TreeViewItem>(rootContainer.ItemContainerGenerator.ContainerFromItem(folder));
                var targetContainer = Assert.IsType<TreeViewItem>(folderContainer.ItemContainerGenerator.ContainerFromItem(target));
                DependencyObject? ancestor = VisualTreeHelper.GetParent(targetContainer);
                while (ancestor is not null and not ScrollViewer)
                    ancestor = VisualTreeHelper.GetParent(ancestor);
                var viewer = Assert.IsAssignableFrom<ScrollViewer>(ancestor);
                double top = targetContainer.TranslatePoint(new Point(0, 0), viewer).Y;
                Assert.InRange(top, 0, viewer.ViewportHeight);
                Assert.True(folder.IsExpanded);
                Assert.Null(rootContainer.ItemContainerGenerator.ContainerFromItem(root.VisualChildren[80]));
            }
            finally
            {
                foreach (ResourceDictionary dictionary in theme) dictionaries.Remove(dictionary);
                managerField.SetValue(null, previousManager);
            }
        });
    }

    private static void AddFiles(SolutionNode parent, int count)
    {
        for (int index = 0; index < count; index++)
        {
            var file = new FileInfo(Path.Combine(Path.GetTempPath(), $"colorvision-reveal-{parent.Name}-{index:D3}.txt"));
            parent.VisualChildren.Add(new FileNode(file) { Parent = parent });
        }
    }

    private static void Layout(FrameworkElement view)
    {
        view.Measure(new Size(380, 520));
        view.Arrange(new Rect(0, 0, 380, 520));
        view.UpdateLayout();
        view.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        view.UpdateLayout();
    }
}
