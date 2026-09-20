using AvalonDock.Layout;
using ColorVision.Copilot;
using ColorVision.Solution;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotLocalFileLinkNavigationTests
{
    [Fact]
    public void StandaloneOpenDoesNotNavigateAnUnrelatedEditor()
    {
        Run(scope =>
        {
            var oldEditor = scope.OpenText(scope.OtherPath);
            var oldText = Editor(oldEditor).Text;
            CopilotLinkProbeOpenAction.Open = _ => new(true, true);

            var opened = CopilotLocalFileLinkNavigator.TryOpen(new(scope.TargetPath, 4, 3), out var error);

            Assert.Equal(1, Editor(oldEditor).TextArea.Caret.Line);
            Assert.Equal(oldText, Editor(oldEditor).Text);
            Assert.False(oldEditor.IsDirty);
            Assert.False(opened);
            Assert.Contains("定位", error);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReferenceNavigatesTheMatchingDockedOrFloatingEditor(bool floating)
    {
        Run(scope =>
        {
            var oldEditor = scope.OpenText(scope.OtherPath);
            AvalonEditControll? targetEditor = null;
            CopilotLinkProbeOpenAction.Open = path =>
            {
                targetEditor = scope.OpenText(path, floating);
                return new(true, true);
            };

            Assert.True(CopilotLocalFileLinkNavigator.TryOpen(new(scope.TargetPath, 4, 3), out var error), error);
            Assert.NotNull(targetEditor);
            Assert.Equal(4, Editor(targetEditor).TextArea.Caret.Line);
            Assert.Equal(3, Editor(targetEditor).TextArea.Caret.Column);
            Assert.Equal(1, Editor(oldEditor).TextArea.Caret.Line);
            Assert.False(targetEditor.IsDirty);
        });
    }

    [Fact]
    public void StandaloneOpenWithoutCoordinatesSucceedsAndKeepsEditorPosition()
    {
        Run(scope =>
        {
            var oldEditor = scope.OpenText(scope.OtherPath);
            CopilotLinkProbeOpenAction.Open = _ => new(true, true);
            Assert.True(CopilotLocalFileLinkNavigator.TryOpen(new(scope.TargetPath, null, null), out var error), error);
            Assert.Equal(1, Editor(oldEditor).TextArea.Caret.Line);
        });
    }

    [Fact]
    public void OpenFailureKeepsTheRouterCause()
    {
        Run(scope =>
        {
            CopilotLinkProbeOpenAction.Open = _ => new(true, false, "测试查看器拒绝了此文件格式。");
            Assert.False(CopilotLocalFileLinkNavigator.TryOpen(new(scope.TargetPath, 4, 3), out var error));
            Assert.Equal("测试查看器拒绝了此文件格式。", error);
        });
    }

    [Fact]
    public void RenderedMarkdownClickNavigatesAndClearsAnEarlierFailure()
    {
        Run(scope =>
        {
            var view = new CopilotMarkdownView();
            var builder = typeof(CopilotMarkdownView).GetMethod("BuildMarkdownDocument", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var document = Assert.IsType<FlowDocument>(builder.Invoke(view, [$"[采集来源](<{scope.TargetPath}:4:3>)"]));
            var paragraph = Assert.IsType<Paragraph>(Assert.Single(document.Blocks.Cast<Block>()));
            var link = Assert.Single(paragraph.Inlines.OfType<Hyperlink>());
            var target = Assert.IsType<CopilotLocalFileLinkTarget>(link.Tag);
            Assert.Equal(scope.TargetPath, target.FilePath);
            var expectedToolTip = CopilotLocalFileLinkNavigator.BuildToolTip(target);
            Assert.Equal(expectedToolTip, link.ToolTip);
            CopilotLinkProbeOpenAction.Open = _ => new(true, false, "测试文件暂时无法打开。");

            link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));

            Assert.Equal("文件引用未完成：测试文件暂时无法打开。", link.ToolTip);
            AvalonEditControll? openedEditor = null;
            CopilotLinkProbeOpenAction.Open = path => { openedEditor = scope.OpenText(path); return new(true, true); };

            link.RaiseEvent(new RoutedEventArgs(Hyperlink.ClickEvent));

            Assert.NotNull(openedEditor);
            Assert.Equal(4, Editor(openedEditor).TextArea.Caret.Line);
            Assert.Equal(3, Editor(openedEditor).TextArea.Caret.Column);
            Assert.Equal(expectedToolTip, link.ToolTip);
        });
    }

    [Theory]
    [InlineData("#L0")]
    [InlineData("#L99999999999999999")]
    [InlineData("#L4C0")]
    [InlineData(":4:99999999999999999")]
    public void InvalidCoordinatesAreNotSilentlyConvertedToPlainFileLinks(string suffix)
    {
        Run(scope => Assert.False(CopilotLocalFileLinkNavigator.TryResolve(scope.TargetPath + suffix, out _)));
    }

    [Theory]
    [InlineData("absolute")]
    [InlineData("relative")]
    [InlineData("uri")]
    [InlineData("encoded-relative")]
    public void ReferenceFormatsResolveTheSameFileAndCoordinates(string format)
    {
        Run(scope =>
        {
            var link = format switch
            {
                "relative" => Path.GetFileName(scope.TargetPath) + ":4:3",
                "uri" => new Uri(scope.TargetPath).AbsoluteUri + "#L4C3",
                "encoded-relative" => Uri.EscapeDataString(Path.GetFileName(scope.TargetPath)) + "#L4C3",
                _ => scope.TargetPath + ":4:3",
            };
            Assert.True(CopilotLocalFileLinkNavigator.TryResolve(link, out var target));
            Assert.Equal(scope.TargetPath, target.FilePath);
            Assert.Equal(4, target.LineNumber);
            Assert.Equal(3, target.ColumnNumber);
        });
    }

    [Fact]
    public void ClickingAReferenceRechecksTheCurrentWorkspace()
    {
        Run(scope =>
        {
            Assert.True(CopilotLocalFileLinkNavigator.TryResolve(scope.TargetPath + ":4", out var target));
            scope.SetWorkspace(Directory.CreateDirectory(Path.Combine(scope.Root, "another-workspace")).FullName);
            var opened = false;
            CopilotLinkProbeOpenAction.Open = _ => { opened = true; return new(true, true); };
            Assert.False(CopilotLocalFileLinkNavigator.TryOpen(target, out _));
            Assert.False(opened);
        });
    }

    private static ICSharpCode.AvalonEdit.TextEditor Editor(AvalonEditControll control) =>
        (ICSharpCode.AvalonEdit.TextEditor)control.FindName("textEditor");

    private static void Run(Action<Scope> test) => StaTest.Run(() =>
    {
        try
        {
            using var scope = new Scope();
            test(scope);
        }
        finally
        {
            Dispatcher.CurrentDispatcher.InvokeShutdown();
        }
    });

    private sealed class Scope : IDisposable
    {
        private static readonly FieldInfo Instance = typeof(SolutionManager).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? _previous = Instance.GetValue(null);
        private readonly SolutionManager _manager = (SolutionManager)RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly LayoutRoot? _previousRoot = WorkspaceManager.layoutRoot;
        private readonly LayoutDocumentPane? _previousPane = WorkspaceManager.LayoutDocumentPane;
        private readonly string _previousSelected = WorkspaceManager.SelectedContentId;
        private readonly List<LayoutDocument> _documents = [];
        public string Root { get; } = Directory.CreateTempSubdirectory("CopilotFileLink-").FullName;
        public string TargetPath => Path.Combine(Root, "采集 记录.copilotlinkprobe");
        public string OtherPath => Path.Combine(Root, "other.txt");

        public Scope()
        {
            File.WriteAllText(TargetPath, "first\nsecond\nthird\nfourth\nfifth");
            File.WriteAllText(OtherPath, "old first\nold second\nold third\nold fourth\nold fifth");
            Instance.SetValue(null, _manager);
            SetWorkspace(Root);
            WorkspaceManager.LayoutDocumentPane = new();
            WorkspaceManager.layoutRoot = new() { RootPanel = new LayoutPanel(WorkspaceManager.LayoutDocumentPane) };
        }

        public void SetWorkspace(string path)
        {
            var explorer = (SolutionExplorer)RuntimeHelpers.GetUninitializedObject(typeof(SolutionExplorer));
            typeof(SolutionExplorer).GetProperty(nameof(SolutionExplorer.DirectoryInfo))!.SetValue(explorer, new DirectoryInfo(path));
            typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_manager, explorer);
        }

        public AvalonEditControll OpenText(string path, bool floating = false)
        {
            var document = EditorDocumentService.Open(path, typeof(ColorVision.Solution.TextEditor), Path.GetFileName(path),
                () => new AvalonEditControll(path), control => control.Dispose());
            if (!_documents.Contains(document))
                _documents.Add(document);
            if (floating)
            {
                WorkspaceManager.LayoutDocumentPane.Children.Remove(document);
                WorkspaceManager.layoutRoot.FloatingWindows.Add(new LayoutDocumentFloatingWindow
                {
                    RootPanel = new LayoutDocumentPaneGroup(new LayoutDocumentPane(document)),
                });
                document.IsActive = true;
            }
            return (AvalonEditControll)document.Content;
        }

        public void Dispose()
        {
            CopilotLinkProbeOpenAction.Open = null;
            foreach (var document in _documents)
                document.Close();
            WorkspaceManager.layoutRoot = _previousRoot!;
            WorkspaceManager.LayoutDocumentPane = _previousPane!;
            WorkspaceManager.OnContentIdSelected(_previousSelected);
            Instance.SetValue(null, _previous);
            var resolved = Path.GetFullPath(Root);
            if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("CopilotFileLink-", StringComparison.Ordinal))
                throw new InvalidOperationException("The fixture directory is outside its temporary root.");
            Directory.Delete(resolved, recursive: true);
        }
    }
}

[FileExtension(".copilotlinkprobe")]
public sealed class CopilotLinkProbeOpenAction : IFileOpenActionProcessor
{
    internal static Func<string, FileOpenRouteResult>? Open { get; set; }
    public int Order => 0;
    public FileOpenRouteResult OpenFile(string filePath) => Open?.Invoke(filePath) ?? FileOpenRouteResult.NotHandled;
}
