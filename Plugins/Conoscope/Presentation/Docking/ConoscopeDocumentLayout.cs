using AvalonDock;
using AvalonDock.Layout;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Conoscope.Presentation.Docking
{
    internal sealed class ConoscopeDocumentLayout
    {
        private readonly DockingManager manager;

        internal ConoscopeDocumentLayout(DockingManager manager)
        {
            this.manager = manager;
        }

        internal IEnumerable<LayoutDocument> Documents => manager.Layout.Descendents().OfType<LayoutDocument>();

        internal bool HasDocuments => Documents.Any();

        internal LayoutDocument? ActiveDocument => Find(manager.ActiveContent)
            ?? Documents.FirstOrDefault(document => document.IsActive)
            ?? Documents.FirstOrDefault(document => document.IsSelected);

        internal LayoutDocument? Find(object? content)
        {
            return content == null ? null : Documents.FirstOrDefault(document => ReferenceEquals(document.Content, content));
        }

        internal void Select(LayoutDocument document)
        {
            if (document.Parent is LayoutDocumentPane pane)
                pane.SelectedContentIndex = pane.IndexOf(document);
            document.IsActive = true;
        }

        internal void Add(LayoutDocument document)
        {
            LayoutDocumentPane? pane = ActiveDocument?.Parent as LayoutDocumentPane
                ?? Documents.Select(item => item.Parent).OfType<LayoutDocumentPane>().FirstOrDefault()
                ?? manager.Layout.Descendents().OfType<LayoutDocumentPane>().FirstOrDefault();
            if (pane == null)
            {
                pane = new LayoutDocumentPane();
                manager.Layout.RootPanel.Children.Add(pane);
            }
            pane.Children.Add(document);
        }

        internal void TrackLifetime(LayoutDocument document, Action? onClosed)
        {
            IDisposable? content = document.Content as IDisposable;
            document.Closed += OnClosed;

            void OnClosed(object? sender, EventArgs args)
            {
                document.Closed -= OnClosed;
                content?.Dispose();
                onClosed?.Invoke();
            }
        }
    }
}
