using AvalonDock.Layout;
using ColorVision.Solution;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Explorer;
using ColorVision.Solution.Workspace;
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

    [Theory]
    [InlineData("#L0")]
    [InlineData("#L99999999999999999")]
    [InlineData("#L4C0")]
    [InlineData(":4:99999999999999999")]
    public void InvalidCoordinatesAreNotSilentlyConvertedToPlainFileLinks(string suffix)
    {
        Run(scope => Assert.False(CopilotLocalFileLinkNavigator.TryResolve(scope.TargetPath + suffix, out _)));
    }

    [Fact]
    public void ExplicitTrustedRootCanOpenAnAbsoluteReferenceWithoutAnOpenWorkspace()
    {
        Run(scope =>
        {
            var trustedRoot = Directory.CreateDirectory(Path.Combine(scope.Root, "personal-instructions")).FullName;
            var trustedPath = Path.Combine(trustedRoot, "personal.copilotlinkprobe");
            File.WriteAllText(trustedPath, "Personal instructions.");
            scope.SetWorkspace(null);
            Assert.False(CopilotLocalFileLinkNavigator.TryResolve(trustedPath, out _));
            Assert.False(CopilotLocalFileLinkNavigator.TryResolve(scope.OtherPath, [trustedRoot], out _));
            Assert.False(CopilotLocalFileLinkNavigator.TryResolve(Path.GetFileName(trustedPath), [trustedRoot], out _));
            var uriWithoutDrive = "file:///" + trustedPath[Path.GetPathRoot(trustedPath)!.Length..].Replace('\\', '/');
            Assert.False(CopilotLocalFileLinkNavigator.TryResolve(uriWithoutDrive, [trustedRoot], out _));
            Assert.True(CopilotLocalFileLinkNavigator.TryResolve(new Uri(trustedPath).AbsoluteUri, [trustedRoot], out var uriTarget));
            Assert.Equal(trustedPath, uriTarget.FilePath);
            Assert.True(CopilotLocalFileLinkNavigator.TryResolve(trustedPath, [trustedRoot], out var target));
            string? openedPath = null;
            CopilotLinkProbeOpenAction.Open = path => { openedPath = path; return new(true, true); };

            Assert.True(CopilotLocalFileLinkNavigator.TryOpen(target, [trustedRoot], out var error), error);

            Assert.Equal(trustedPath, openedPath);
            openedPath = null;
            Assert.False(CopilotLocalFileLinkNavigator.TryOpen(new(scope.OtherPath, null, null), [trustedRoot], out _));
            Assert.Null(openedPath);
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

        public void SetWorkspace(string? path)
        {
            if (path == null)
            {
                typeof(SolutionManager).GetField("_CurrentSolutionExplorer", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(_manager, null);
                return;
            }
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
