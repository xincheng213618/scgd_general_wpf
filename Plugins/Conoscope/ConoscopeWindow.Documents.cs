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
        private ConoscopeView AddConoscopeView(string? filePath, bool activate)
        {
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                string existingContentId = GetContentId(filePath);
                LayoutDocument? existingDocument = ViewDocumentPane.Children
                    .OfType<LayoutDocument>()
                    .FirstOrDefault(item => item.ContentId == existingContentId);
                if (existingDocument?.Content is ConoscopeView existingView)
                {
                    SelectDocument(existingDocument);
                    ConoscopeModuleService.Activate(existingView);
                    return existingView;
                }
            }

            ConoscopeView view = new ConoscopeView();
            if (!string.IsNullOrWhiteSpace(filePath))
            {
                view.OpenConoscope(filePath);
            }

            LayoutDocument layoutDocument = new LayoutDocument
            {
                Title = string.IsNullOrWhiteSpace(filePath) ? "Conoscope" : Path.GetFileName(filePath),
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
                }
            };
            layoutDocument.Closing += (s, e) => view.Dispose();

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

            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && ActiveView != null;
            btnOpenActiveView3D.IsEnabled = ActiveView != null;
            btnOpenActiveViewCie.IsEnabled = ActiveView != null;
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

        private static string GetContentId(string filePath)
        {
            return "StandaloneConoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
        }
    }
}
