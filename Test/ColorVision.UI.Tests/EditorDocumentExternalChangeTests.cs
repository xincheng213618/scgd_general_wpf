using AvalonDock.Layout;
using ColorVision.Solution.Editor.AvalonEditor;
using ColorVision.Solution.Workspace;
using ICSharpCode.AvalonEdit;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class EditorDocumentExternalChangeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BackupStyleReplacementKeepsTheOriginalDocumentPath(bool dirty)
    {
        WithOpenDocument((path, document, editor, text) =>
        {
            if (dirty)
                text.Document.Replace(0, text.Document.TextLength, "my draft");

            DateTime originalTime = File.GetLastWriteTimeUtc(path);
            string backup = path + ".bak";
            File.Move(path, backup);
            object session = QueueRename(document, path, backup);
            File.WriteAllText(path, "external");
            File.SetLastWriteTimeUtc(path, originalTime);

            if (dirty)
            {
                Assert.Throws<IOException>(() => editor.Save());
                Assert.True(editor.IsDirty);
                Assert.Equal("my draft", text.Text);
            }
            else
            {
                CheckExternalChange(session);
                Assert.Equal("external", text.Text);
            }

            Assert.True(EditorDocumentService.TryGetFilePath(document, out string currentPath));
            Assert.Equal(path, currentPath);
            Assert.Equal("external", File.ReadAllText(path));
            Assert.Equal("original", File.ReadAllText(backup));
        });
    }

    [Theory]
    [InlineData("renamed.txt")]
    [InlineData("SOURCE.txt")]
    public void RenamedDocumentFollowsItsFile(string newName)
    {
        WithOpenDocument((path, document, editor, text) =>
        {
            string destination = Path.Combine(Path.GetDirectoryName(path)!, newName);
            File.Move(path, destination);
            object session = QueueRename(document, path, destination);
            text.Document.Replace(0, text.Document.TextLength, "my draft");
            if (!File.Exists(path))
            {
                Assert.False(Assert.IsType<bool>(InvokeSession(session, "TrySave", [false])));
                Assert.False(File.Exists(path));
                Assert.Equal("original", File.ReadAllText(destination));
            }
            CheckExternalChange(session);

            Assert.True(EditorDocumentService.TryGetFilePath(document, out string currentPath));
            Assert.Equal(destination, currentPath);
            Assert.True(editor.Save());
            Assert.Equal("my draft", File.ReadAllText(destination));
        });
    }

    private static void WithOpenDocument(Action<string, LayoutDocument, AvalonEditControll, TextEditor> test)
    {
        WpfTestHost.Invoke(() =>
        {
            LayoutRoot previousRoot = WorkspaceManager.layoutRoot;
            LayoutDocumentPane previousPane = WorkspaceManager.LayoutDocumentPane;
            var root = new LayoutRoot { RootPanel = new LayoutPanel() };
            var pane = new LayoutDocumentPane();
            root.RootPanel.Children.Add(pane);
            WorkspaceManager.layoutRoot = root;
            WorkspaceManager.LayoutDocumentPane = pane;
            DirectoryInfo directory = Directory.CreateTempSubdirectory("ColorVision-DocumentRename-");
            string path = Path.Combine(directory.FullName, "source.txt");
            var theme = new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml", UriKind.Relative) };
            Application.Current.Resources.MergedDictionaries.Add(theme);
            try
            {
                File.WriteAllText(path, "original");
                using var editor = new AvalonEditControll(path);
                LayoutDocument document = EditorDocumentService.Open(path, typeof(EditorDocumentExternalChangeTests), "source.txt", () => editor);
                try
                {
                    // Deliver the watcher callback explicitly and process one check without timing assertions.
                    GetWatcher(GetSession(document)).EnableRaisingEvents = false;
                    test(path, document, editor, Assert.IsType<TextEditor>(editor.FindName("textEditor")));
                }
                finally
                {
                    if (editor.IsDirty) Assert.True(editor.ReloadFromDisk());
                    document.Close();
                }
            }
            finally
            {
                WorkspaceManager.layoutRoot = previousRoot;
                WorkspaceManager.LayoutDocumentPane = previousPane;
                Application.Current.Resources.MergedDictionaries.Remove(theme);
                foreach (FileInfo file in directory.EnumerateFiles()) file.Delete();
                directory.Delete();
            }
        });
    }

    private static object GetSession(LayoutDocument document)
    {
        var sessions = (IDictionary)typeof(EditorDocumentService).GetField("_sessions", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        return sessions[document]!;
    }

    private static object QueueRename(LayoutDocument document, string oldPath, string newPath)
    {
        object session = GetSession(document);
        var args = new RenamedEventArgs(WatcherChangeTypes.Renamed, Path.GetDirectoryName(oldPath)!, Path.GetFileName(newPath), Path.GetFileName(oldPath));
        InvokeSession(session, "ResourceWatcher_Renamed", [GetWatcher(session), args]);
        // Process the queued watcher event before the lower-priority debounce timer.
        Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.Normal);
        return session;
    }

    private static void CheckExternalChange(object session) => InvokeSession(session, "ExternalChangeTimer_Tick", [null, EventArgs.Empty]);

    private static FileSystemWatcher GetWatcher(object session) => Assert.IsType<FileSystemWatcher>(session.GetType()
        .GetField("_resourceWatcher", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session));

    private static object? InvokeSession(object session, string method, object?[] args) =>
        session.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.Invoke(session, args);
}
