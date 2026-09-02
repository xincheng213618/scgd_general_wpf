using ColorVision.Solution.Explorer;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace ColorVision.UI.Tests;

/// <summary>Uses production presentation markup with synthetic nodes, without starting a workspace or MainWindow.</summary>
public sealed class SolutionExplorerPresentationTests
{
    [Fact]
    public void CollapseAllResetsHiddenExpandedDescendantsWithoutLoadingLazyFolders()
    {
        WpfTestHost.Invoke(() =>
        {
            var root = new SolutionNode { Name = "Workspace", IsExpanded = false };
            var closedBranch = new SolutionNode { Name = "Closed", IsExpanded = false };
            var hiddenExpandedChild = new SolutionNode { Name = "Hidden", IsExpanded = true };
            using var lazyFolder = new FolderNode(new DirectoryInfo(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"))));
            root.AddChild(closedBranch);
            closedBranch.AddChild(hiddenExpandedChild);
            root.AddChild(lazyFolder);

            SolutionTreeNavigationService.CollapseDescendants(root);

            Assert.True(root.IsExpanded);
            Assert.False(closedBranch.IsExpanded);
            Assert.False(hiddenExpandedChild.IsExpanded);
            Assert.False(lazyFolder.IsExpanded);
            Assert.False(lazyFolder.AreChildrenLoaded);
        });
    }

    [Fact]
    public async Task ActiveDocumentResolutionKeepsLoadedIdentityAndRejectsNeighborAndRelativePaths()
    {
        (FolderNode root, FileNode file) = WpfTestHost.Invoke(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), "ColorVision.Explorer.Navigation", Guid.NewGuid().ToString("N"));
            var root = new FolderNode(new DirectoryInfo(directory));
            var file = new FileNode(new FileInfo(Path.Combine(directory, "measurement.cvraw")));
            root.AddChild(file);
            return (root, file);
        });
        try
        {
            SolutionNode? resolved = await WpfTestHost.Invoke(() => SolutionTreeNavigationService.ResolvePathAsync(root, file.FullPath.ToUpperInvariant()));
            WpfTestHost.Invoke(() =>
            {
                Assert.Same(file, resolved);
                Assert.False(root.IsExpanded);
                Assert.False(root.AreChildrenLoaded);
                Assert.False(SolutionTreeNavigationService.CanResolvePath(root, Path.Combine(root.FullPath + "-other", "measurement.cvraw")));
                Assert.False(SolutionTreeNavigationService.CanResolvePath(root, "measurement.cvraw"));
            });
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WpfTestHost.Invoke(() =>
                SolutionTreeNavigationService.ResolvePathAsync(root, file.FullPath, cancellation.Token)));
        }
        finally
        {
            WpfTestHost.Invoke(root.Dispose);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void NarrowExplorerKeepsSelectionAndScrollbarLegibleAcrossThemeReplacement(bool initiallyDark, bool fileSystemView)
    {
        WpfTestHost.Invoke(() =>
        {
            using var scene = new ExplorerScene(initiallyDark, fileSystemView);
            var selection = new SolutionSelectionService();
            selection.SelectSingle(scene.SelectedFile);
            foreach (bool dark in new[] { initiallyDark, !initiallyDark, initiallyDark })
            {
                scene.ReplaceTheme(dark);
                scene.Arrange();
                Assert.Same(scene.Root, Assert.Single(scene.Tree.Items));
                Assert.Same(scene.SelectedFile, Assert.Single(selection.CommandNodes));
                TreeViewItem selected = scene.Container(scene.SelectedFile);
                Border row = Assert.IsType<Border>(selected.Template.FindName("Bd", selected));
                Assert.True(row.ActualWidth >= scene.Tree.ActualWidth - 22,
                    "Selection should extend across the available tree row.");
                Assert.InRange(row.ActualHeight, 20, 28);
                Border tint = Assert.IsType<Border>(selected.Template.FindName("SelectionBackground", selected));
                Border accent = Assert.IsType<Border>(selected.Template.FindName("SelectionAccent", selected));
                Assert.Equal(Visibility.Visible, tint.Visibility);
                Assert.Equal(Visibility.Visible, accent.Visibility);
                Color selectedBackground = Composite(ColorOf(tint.Background), tint.Opacity, ColorOf(scene.Tree.Background));
                Assert.NotEqual(ColorOf(scene.Tree.Background), selectedBackground);
                Assert.InRange(Math.Abs(selectedBackground.R - selectedBackground.G), 0, 1);
                Assert.InRange(Math.Abs(selectedBackground.G - selectedBackground.B), 0, 1);
                Assert.Equal(new CornerRadius(4), tint.CornerRadius);
                Assert.Equal(new Thickness(2, 4, 0, 4), accent.Margin);
                ScrollViewer viewer = Assert.Single(Descendants<ScrollViewer>(scene.Tree), scroll => scroll.TemplatedParent == scene.Tree);
                Assert.Equal(viewer.ViewportWidth, tint.ActualWidth, 1);
                viewer.ScrollToHorizontalOffset(40);
                scene.Arrange();
                Assert.True(viewer.HorizontalOffset > 0);
                Assert.Equal(viewer.HorizontalOffset, Assert.IsType<TranslateTransform>(tint.RenderTransform).X, 1);
                Assert.Equal(viewer.HorizontalOffset, Assert.IsType<TranslateTransform>(accent.RenderTransform).X, 1);
                viewer.ScrollToHorizontalOffset(0);
                scene.Arrange();
                Assert.True(ContrastRatio(ColorOf(selected.Foreground), selectedBackground) >= 3,
                    "Selected text must remain distinguishable in both palettes.");
                scene.AssertToolbarFits();
                scene.AssertExpanderTracksFolderState();

                ScrollBar vertical = Assert.Single(Descendants<ScrollBar>(scene.Tree),
                    bar => bar.Orientation == Orientation.Vertical && bar.Visibility == Visibility.Visible);
                RenderTargetBitmap bitmap = scene.Render();
                Assert.Equal(640, bitmap.PixelWidth);
                Assert.Equal(1240, bitmap.PixelHeight);
                scene.SavePreview(bitmap, dark);
                Assert.NotNull(vertical.Style);
                Assert.Equal(0.35, Assert.IsType<SolidColorBrush>(vertical.OpacityMask).Opacity, 2);
                Assert.Equal(BaseValueSource.ParentTemplate, DependencyPropertyHelper.GetValueSource(vertical, UIElement.OpacityProperty).BaseValueSource);
                Assert.NotNull(vertical.Template);
                Assert.True(vertical.Maximum > vertical.Minimum);
            }
            ScrollBar originalBar = Assert.Single(Descendants<ScrollBar>(scene.Tree), bar => bar.Orientation == Orientation.Vertical);
            ControlTemplate originalTemplate = originalBar.Template;
            Style originalStyle = originalBar.Style;
            ExplorerScrollBarBehavior.SetIsEnabled(scene.Tree, false);
            Assert.Null(originalBar.OpacityMask);
            ExplorerScrollBarBehavior.SetIsEnabled(scene.Tree, true);
            scene.Arrange();
            Assert.Same(originalTemplate, originalBar.Template);
            Assert.Same(originalStyle, originalBar.Style);
            Assert.Equal(0.35, Assert.IsType<SolidColorBrush>(originalBar.OpacityMask).Opacity, 2);
            FolderNode selectedFolder = Assert.IsAssignableFrom<FolderNode>(scene.Root.VisualChildren[0]);
            selection.SelectSingle(selectedFolder);
            foreach (bool expanded in new[] { true, false })
            {
                selectedFolder.IsExpanded = expanded;
                scene.Arrange();
                TreeViewItem folderContainer = scene.Container(selectedFolder);
                Assert.Equal(Visibility.Visible, ((Border)folderContainer.Template.FindName("SelectionAccent", folderContainer)).Visibility);
                scene.SavePreview(scene.Render(), initiallyDark, expanded ? "folder-expanded" : "folder-collapsed");
            }
            selectedFolder.IsExpanded = true;
            selection.Clear();
            scene.Arrange();
            Assert.False(scene.SelectedFile.IsMultiSelected);
            Assert.Equal(Visibility.Collapsed, ((Border)scene.Container(scene.SelectedFile).Template.FindName("SelectionBackground", scene.Container(scene.SelectedFile))).Visibility);
        });
    }

    private static Color ColorOf(Brush brush) => Assert.IsType<SolidColorBrush>(brush).Color;

    private static Color Composite(Color foreground, double opacity, Color background)
        => Color.FromRgb((byte)(foreground.R * opacity + background.R * (1 - opacity)),
            (byte)(foreground.G * opacity + background.G * (1 - opacity)),
            (byte)(foreground.B * opacity + background.B * (1 - opacity)));

    private static double ContrastRatio(Color first, Color second)
    {
        static double Channel(byte value)
        {
            double normalized = value / 255d;
            return normalized <= 0.04045 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
        static double Luminance(Color color) => 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        double firstLuminance = Luminance(first);
        double secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
                yield return match;
            foreach (T descendant in Descendants<T>(child))
                yield return descendant;
        }
    }

    private sealed class ExplorerScene : IDisposable
    {
        private readonly List<ResourceDictionary> _applicationResources;
        private readonly ResourceDictionary _theme = new();
        private readonly Window? _host;
        private readonly bool _fileSystemView;
        internal UserControl Control { get; }
        internal TreeView Tree { get; }
        internal FolderNode Root { get; }
        internal FileNode SelectedFile { get; }

        internal ExplorerScene(bool dark, bool fileSystemView)
        {
            _fileSystemView = fileSystemView;
            _applicationResources = Application.Current.Resources.MergedDictionaries.ToList();
            Application.Current.Resources.MergedDictionaries.Clear();
            Application.Current.Resources.MergedDictionaries.Add(_theme);
            ReplaceTheme(dark);
            try
            {
                Control = LoadPresentation();
                Control.Resources.MergedDictionaries.Insert(0, _theme);
                Tree = Assert.IsType<TreeView>(Control.FindName("SolutionTreeView"));
                foreach (ICommand command in new ICommand[] { SolutionNavigationCommands.SyncWithActiveDocument,
                    SolutionNavigationCommands.CollapseAll, SolutionNavigationCommands.Refresh, ApplicationCommands.Properties })
                    Tree.CommandBindings.Add(new CommandBinding(command, (_, _) => { }, (_, args) => args.CanExecute = true));
                Control.DataContext = new { CurrentSolutionExplorer = new { DirectoryInfo = new DirectoryInfo(@"C:\ColorVision\Inspection workspace") } };
                // Nonexistent paths ensure FolderNode never starts background directory loading.
                string syntheticRoot = Path.Combine(Path.GetTempPath(), "ColorVision.Explorer.Presentation", Guid.NewGuid().ToString("N"));
                Root = CreateFolder(syntheticRoot, isRoot: true);
                Root.Name = "Inspection workspace";
                Root.IsExpanded = true;
                var images = CreateFolder(Path.Combine(syntheticRoot, "Images"));
                images.IsExpanded = true;
                Root.AddChild(images);
                SelectedFile = new FileNode(new FileInfo(Path.Combine(images.FullPath, "20260902_Calibration_measurement_with_a_long_file_name.cvraw")));
                images.AddChild(SelectedFile);
                images.AddChild(new FileNode(new FileInfo(Path.Combine(images.FullPath, "Reference.tif"))));
                foreach (string name in new[] { "Algorithms", "Configurations", "Examples", "Reports" })
                    Root.AddChild(CreateFolder(Path.Combine(syntheticRoot, name)));
                for (int index = 1; index <= 30; index++)
                    Root.AddChild(new FileNode(new FileInfo(Path.Combine(syntheticRoot, $"inspection_{index:00}.json"))));
                Tree.ItemsSource = new[] { Root };
                ((RadioButton)Control.FindName(fileSystemView ? "FileSystemViewButton" : "SolutionViewButton")).IsChecked = true;
                _host = new Window
                {
                    Content = Control, Width = 320, Height = 620, Left = -10000, Top = -10000,
                    WindowStartupLocation = WindowStartupLocation.Manual, WindowStyle = WindowStyle.None,
                    ResizeMode = ResizeMode.NoResize, ShowActivated = false, ShowInTaskbar = false, Opacity = 0,
                };
                _host.Show();
                Arrange();
                Assert.True(Control.IsLoaded);
                Assert.False(_host.IsActive);
            }
            catch
            {
                _host?.Close();
                RestoreApplicationResources();
                throw;
            }
        }

        private FolderNode CreateFolder(string path, bool isRoot = false) => _fileSystemView
            ? new FileSystemFolderNode(new DirectoryInfo(path), isRoot)
            : new FolderNode(new DirectoryInfo(path));

        internal void ReplaceTheme(bool dark)
        {
            _theme.MergedDictionaries.Clear();
            foreach (string path in new[]
            {
                $"/HandyControl;component/Themes/basic/colors/{(dark ? "colorsdark" : "colors")}.xaml",
                "/HandyControl;component/Themes/Theme.xaml",
                $"/ColorVision.Themes;component/Themes/{(dark ? "Dark" : "White")}.xaml",
                "/ColorVision.Themes;component/Themes/Base.xaml",
                "/ColorVision.Themes;component/Themes/Menu.xaml",
                "/ColorVision.Themes;component/Themes/GroupBox.xaml",
                "/ColorVision.Themes;component/Themes/Icons.xaml",
            })
                _theme.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri(path, UriKind.Relative) });
        }

        internal void Arrange()
        {
            for (int pass = 0; pass < 2; pass++)
            {
                Control.ApplyTemplate();
                Control.Measure(new Size(320, 620));
                Control.Arrange(new Rect(0, 0, 320, 620));
                Control.UpdateLayout();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            }
        }

        internal TreeViewItem Container(SolutionNode node)
            => Assert.Single(Descendants<TreeViewItem>(Tree), item => ReferenceEquals(item.DataContext, node));

        internal void AssertToolbarFits()
        {
            RadioButton solutionMode = Assert.IsType<RadioButton>(Control.FindName("SolutionViewButton"));
            RadioButton fileMode = Assert.IsType<RadioButton>(Control.FindName("FileSystemViewButton"));
            WrapPanel toolbar = Assert.IsType<WrapPanel>(VisualTreeHelper.GetParent(solutionMode));
            Assert.Equal(7, toolbar.Children.OfType<ButtonBase>().Count());
            double top = solutionMode.TransformToAncestor(Control).Transform(new Point()).Y;
            foreach (ButtonBase button in toolbar.Children.OfType<ButtonBase>())
            {
                Rect bounds = button.TransformToAncestor(Control).TransformBounds(new Rect(button.RenderSize));
                Assert.InRange(bounds.Left, 0, 320);
                Assert.InRange(bounds.Right, bounds.Left + 1, 320);
                Assert.Equal(top, bounds.Top, 1);
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            }
            Assert.Equal(!_fileSystemView, solutionMode.IsChecked);
            Assert.Equal(_fileSystemView, fileMode.IsChecked);
            fileMode.IsChecked = true;
            Assert.False(solutionMode.IsChecked);
            solutionMode.IsChecked = true;
            Assert.False(fileMode.IsChecked);
            fileMode.IsChecked = _fileSystemView;
            TextBlock path = Assert.IsType<TextBlock>(Control.FindName("WorkspacePathText"));
            Assert.Equal(TextTrimming.CharacterEllipsis, path.TextTrimming);
            Assert.NotEmpty(path.Text);
        }

        internal void AssertExpanderTracksFolderState()
        {
            FolderNode folder = Assert.IsAssignableFrom<FolderNode>(Root.VisualChildren[0]);
            TreeViewItem container = Container(folder);
            ToggleButton expander = Assert.IsType<ToggleButton>(container.Template.FindName("Expander", container));
            string expandedPath = ((System.Windows.Shapes.Path)expander.Template.FindName("ExpandGlyph", expander)).Data.ToString();
            Assert.True(folder.IsExpanded);
            expander.SetCurrentValue(ToggleButton.IsCheckedProperty, false);
            Arrange();
            Assert.False(folder.IsExpanded);
            Assert.NotEqual(expandedPath, ((System.Windows.Shapes.Path)expander.Template.FindName("ExpandGlyph", expander)).Data.ToString());
            expander.SetCurrentValue(ToggleButton.IsCheckedProperty, true);
            Arrange();
            Assert.True(folder.IsExpanded);
            Assert.Equal(expandedPath, ((System.Windows.Shapes.Path)expander.Template.FindName("ExpandGlyph", expander)).Data.ToString());
            Assert.True(SelectedFile.IsMultiSelected);
        }

        internal RenderTargetBitmap Render()
        {
            var bitmap = new RenderTargetBitmap(640, 1240, 192, 192, PixelFormats.Pbgra32);
            bitmap.Render(Control);
            return bitmap;
        }

        internal void SavePreview(RenderTargetBitmap bitmap, bool dark, string state = "")
        {
            string? outputDirectory = Environment.GetEnvironmentVariable("COLORVISION_EXPLORER_PREVIEW_DIR");
            if (string.IsNullOrWhiteSpace(outputDirectory))
                return;
            Directory.CreateDirectory(outputDirectory);
            string suffix = string.IsNullOrEmpty(state) ? string.Empty : $"-{state}";
            string path = Path.Combine(outputDirectory, $"{(_fileSystemView ? "file-system" : "solution")}-explorer-{(dark ? "dark" : "light")}-320{suffix}.png");
            using FileStream stream = File.Create(path);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            encoder.Save(stream);
        }

        public void Dispose()
        {
            Tree.ItemsSource = null;
            if (_host != null)
            {
                _host.Content = null;
                _host.Close();
            }
            foreach (FolderNode folder in Root.VisualChildren.OfType<FolderNode>())
                folder.Dispose();
            Root.Dispose();
            Control.Resources.MergedDictionaries.Remove(_theme);
            RestoreApplicationResources();
        }

        private void RestoreApplicationResources()
        {
            Application.Current.Resources.MergedDictionaries.Clear();
            foreach (ResourceDictionary resources in _applicationResources)
                Application.Current.Resources.MergedDictionaries.Add(resources);
        }

        private static UserControl LoadPresentation([CallerFilePath] string testPath = "")
        {
            string path = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testPath)!, "..", "..", "UI", "ColorVision.Solution", "TreeViewControl.xaml"));
            XDocument document = XDocument.Load(path);
            XElement root = document.Root!;
            XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
            root.Attribute(xaml + "Class")?.Remove();
            foreach (XAttribute declaration in root.Attributes().Where(attribute => attribute.IsNamespaceDeclaration))
            {
                if (declaration.Value.StartsWith("clr-namespace:", StringComparison.Ordinal)
                    && !declaration.Value.Contains(";assembly=", StringComparison.Ordinal))
                    declaration.Value += ";assembly=ColorVision.Solution";
            }
            foreach (XElement element in root.DescendantsAndSelf())
            {
                string namespaceName = element.Name.NamespaceName;
                if (namespaceName.StartsWith("clr-namespace:", StringComparison.Ordinal)
                    && !namespaceName.Contains(";assembly=", StringComparison.Ordinal))
                    element.Name = XName.Get(element.Name.LocalName, namespaceName + ";assembly=ColorVision.Solution");
                foreach (XAttribute attribute in element.Attributes().Where(attribute =>
                    attribute.Name.NamespaceName.StartsWith("clr-namespace:", StringComparison.Ordinal)
                    && !attribute.Name.NamespaceName.Contains(";assembly=", StringComparison.Ordinal)).ToList())
                {
                    element.SetAttributeValue(XName.Get(attribute.Name.LocalName,
                        attribute.Name.NamespaceName + ";assembly=ColorVision.Solution"), attribute.Value);
                    attribute.Remove();
                }
            }
            HashSet<string> eventNames = new(StringComparer.Ordinal)
            {
                "Initialized", "Loaded", "Unloaded", "Click", "Checked", "Unchecked", "TextChanged",
                "ContextMenuOpening", "SelectedItemChanged", "PreviewKeyDown", "RequestBringIntoView",
            };
            foreach (XAttribute attribute in root.DescendantsAndSelf().Attributes()
                .Where(attribute => attribute.Name.Namespace == XNamespace.None && eventNames.Contains(attribute.Name.LocalName)).ToList())
                attribute.Remove();
            foreach (XElement setter in root.Descendants().Where(element => element.Name.LocalName == "EventSetter").ToList())
                setter.Remove();
            return Assert.IsType<UserControl>(XamlReader.Parse(document.ToString()));
        }
    }
}
