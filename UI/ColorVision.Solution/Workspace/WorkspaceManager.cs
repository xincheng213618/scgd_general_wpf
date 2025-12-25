using AvalonDock.Layout;
using System.Windows;

namespace ColorVision.Solution.Workspace
{
    public static class WorkspaceManager
    {
        public static LayoutDocument? FindDocumentById(object parent, string contentId)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child is LayoutDocument document && document.ContentId == contentId)
                    {
                        return document;
                    }
                    else
                    {
                        var found = FindDocumentById(child, contentId);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }



        public static LayoutDocument? FindDocumentActive(object parent)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child is LayoutDocument document && document.IsActive)
                    {

                        return document;
                    }
                    else
                    {
                        var found = FindDocumentActive(child);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }

        public static ILayoutContainer? FindParentContainer(object parent, LayoutDocument targetDocument)
        {
            if (parent is ILayoutContainer container)
            {
                foreach (var child in container.Children)
                {
                    if (child == targetDocument)
                    {
                        return container;
                    }
                    else
                    {
                        var found = FindParentContainer(child, targetDocument);
                        if (found != null)
                        {
                            return found;
                        }
                    }
                }
            }
            return null;
        }

        public static WorkspaceMainView SolutionView { get; set; }

        public static LayoutRoot layoutRoot { get; set; }

        public static LayoutDocumentPane LayoutDocumentPane { get; set; }

        public static void SelectContentId(string ContentId)
        {
            var existingDocument = WorkspaceManager.FindDocumentById(layoutRoot, ContentId);

            if (existingDocument != null)
            {
                if (existingDocument.Parent is LayoutDocumentPane layoutDocumentPane)
                {
                    layoutDocumentPane.SelectedContentIndex = layoutDocumentPane.IndexOf(existingDocument); ;
                }
                else if (existingDocument.Parent is LayoutFloatingWindow layoutFloatingWindow)
                {
                    var window = Window.GetWindow(layoutFloatingWindow);
                    if (window != null)
                    {
                        window.Activate();
                    }
                }
            }
        }

        public static event  EventHandler<string>? ContentIdSelected;

        public static void OnContentIdSelected(string contentId)
        {
            ContentIdSelected?.Invoke(SolutionView, contentId);
        }

        public static List <Action> DealyLoad { get; set; } = new List<Action>();
    }
}
