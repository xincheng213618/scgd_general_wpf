using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using Conoscope.Core;
using System;
using System.IO;
using System.Linq;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private ConoscopeView AddConoscopeView(string? filePath, bool activate, string? exposureSummary = null, ConoscopeView? reuseView = null)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string existingContentId = GetContentId(filePath);
                LayoutDocument? existingDocument = ViewDocumentPane.Children
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(item => item.ContentId == existingContentId);
                if (existingDocument?.Content is ConoscopeView existingView && !ReferenceEquals(existingView, reuseView))
                {
                    SelectDocument(existingDocument);
                    ConoscopeModuleService.Activate(existingView);
                    return existingView;
                }
            }

            if (reuseView != null && !string.IsNullOrWhiteSpace(filePath))
            {
                LayoutDocument? reuseDocument = GetDocument(reuseView);
                if (reuseDocument != null)
                {
                    reuseView.OpenConoscope(filePath, exposureSummary);
                    reuseDocument.Title = Path.GetFileName(filePath);
                    reuseDocument.ContentId = GetContentId(filePath);
                    if (activate)
                    {
                        SelectDocument(reuseDocument);
                    }
                    else
                    {
                        RefreshActiveViewUi();
                    }

                    return reuseView;
                }
            }

            ConoscopeView view = new ConoscopeView();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                view.OpenConoscope(filePath, exposureSummary);
            }

            LayoutDocument layoutDocument = new LayoutDocument
            {
                Title = string.IsNullOrWhiteSpace(filePath) ? Properties.Resources.WindowTitleConoscope : Path.GetFileName(filePath),
                ContentId = string.IsNullOrWhiteSpace(filePath) ? $"StandaloneConoscope:{Guid.NewGuid():N}" : GetContentId(filePath),
                Content = view,
                CanClose = true,
                CanFloat = true
            };

            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    ConoscopeModuleService.Activate(view);
                    RefreshActiveViewUi();
                }
            };
            layoutDocument.Closing += (s, e) =>
            {
                view.Dispose();
                Dispatcher.BeginInvoke(RefreshActiveViewUi);
            };

            ViewDocumentPane.Children.Add(layoutDocument);
            if (activate)
            {
                SelectDocument(layoutDocument);
            }

            return view;
        }

        private void SelectDocument(LayoutDocument document)
        {
            ViewDocumentPane.SelectedContentIndex = ViewDocumentPane.IndexOf(document);
            document.IsActive = true;
            if (document.Content is ConoscopeView view)
            {
                ConoscopeModuleService.Activate(view);
            }

            RefreshActiveViewUi();
        }

        private ConoscopeView? GetActiveView()
        {
            LayoutDocument? activeDocument = ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => item.IsActive);

            if (activeDocument?.Content is ConoscopeView activeView)
            {
                return activeView;
            }

            int selectedIndex = ViewDocumentPane.SelectedContentIndex;
            if (selectedIndex >= 0 && selectedIndex < ViewDocumentPane.Children.Count
                && ViewDocumentPane.Children[selectedIndex] is LayoutDocument selectedDocument
                && selectedDocument.Content is ConoscopeView selectedView)
            {
                return selectedView;
            }

            return null;
        }

        private ConoscopeView[] GetOpenViews()
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .Select(item => item.Content as ConoscopeView)
                .Where(item => item != null)
                .Cast<ConoscopeView>()
                .ToArray();
        }

        private LayoutDocument? GetDocument(ConoscopeView view)
        {
            return ViewDocumentPane.Children
                .OfType<LayoutDocument>()
                .FirstOrDefault(item => ReferenceEquals(item.Content, view));
        }

        private static string GetContentId(string filePath)
        {
            return "StandaloneConoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
        }
    }
}
