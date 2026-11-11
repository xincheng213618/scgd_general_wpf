using AvalonDock.Layout;
using ColorVision.Solution.Workspace;
using System.IO;
using System.Windows;

namespace ColorVision.UI.Tests;

public sealed class EditorDocumentCloseTests
{
    [Fact]
    public void AbandonedWindowClosePreflightAsksAgainBeforeDiscardingTheDocument()
    {
        WpfTestHost.Invoke(() =>
        {
            LayoutRoot previousRoot = WorkspaceManager.layoutRoot;
            LayoutDocumentPane previousPane = WorkspaceManager.LayoutDocumentPane;
            LayoutRoot root = new() { RootPanel = new LayoutPanel() };
            LayoutDocumentPane pane = new();
            root.RootPanel.Children.Add(pane);
            WorkspaceManager.layoutRoot = root;
            WorkspaceManager.LayoutDocumentPane = pane;
            var draft = new DirtyDocument();
            LayoutDocument? document = null;
            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"ColorVision-Draft-{Guid.NewGuid():N}.txt");
                document = EditorDocumentService.Open(path, typeof(EditorDocumentCloseTests), "Draft", () => draft);
                Assert.True(EditorDocumentService.TryCloseAllDocuments(title =>
                {
                    Assert.Equal("Draft", title);
                    return MessageBoxResult.No;
                }));
                Assert.Same(pane, document.Parent);
                Assert.True(draft.IsDirty);

                // A later close attempt must ask again if the previous window close was canceled.
                int prompts = 0;
                Assert.False(EditorDocumentService.TryCloseAllDocuments(_ =>
                {
                    prompts++;
                    return MessageBoxResult.Cancel;
                }));
                Assert.Equal(1, prompts);
                Assert.Same(pane, document.Parent);
                Assert.True(draft.IsDirty);
            }
            finally
            {
                try
                {
                    draft.MarkClean();
                    document?.Close();
                }
                finally
                {
                    WorkspaceManager.layoutRoot = previousRoot;
                    WorkspaceManager.LayoutDocumentPane = previousPane;
                }
            }
        });
    }

    private sealed class DirtyDocument : IEditorDocumentContent
    {
        public bool IsDirty { get; private set; } = true;
        public bool CanSave => true;
        public event EventHandler? DocumentStateChanged;

        public bool Save()
        {
            MarkClean();
            return true;
        }

        public void MarkClean()
        {
            IsDirty = false;
            DocumentStateChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
