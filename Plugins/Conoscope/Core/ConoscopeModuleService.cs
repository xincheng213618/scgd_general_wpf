using AvalonDock.Layout;
using ColorVision.Common.Utilities;
using ColorVision.Solution.Workspace;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Conoscope.Core
{
    internal static class ConoscopeModuleService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ConoscopeModuleService));
        private static readonly List<WeakReference<ConoscopeView>> Views = new();

        public static ConoscopeView? ActiveView { get; private set; }

        public static void Register(ConoscopeView view)
        {
            CleanupViews();
            if (!Views.Any(item => item.TryGetTarget(out var target) && ReferenceEquals(target, view)))
            {
                Views.Add(new WeakReference<ConoscopeView>(view));
            }

            ActiveView = view;
        }

        public static void Unregister(ConoscopeView view)
        {
            Views.RemoveAll(item => !item.TryGetTarget(out var target) || ReferenceEquals(target, view));
            if (ReferenceEquals(ActiveView, view))
            {
                ActiveView = Views.Select(item => item.TryGetTarget(out var target) ? target : null).FirstOrDefault(item => item != null);
            }
        }

        public static void Activate(ConoscopeView view)
        {
            CleanupViews();
            if (Views.Any(item => item.TryGetTarget(out var target) && ReferenceEquals(target, view)))
            {
                ActiveView = view;
            }
        }

        public static void RefreshAllConoscopeConfiguration()
        {
            CleanupViews();
            foreach (ConoscopeView view in Views.Select(item => item.TryGetTarget(out var target) ? target : null).Where(item => item != null)!)
            {
                view.RefreshConoscopeConfiguration();
            }
        }

        public static void OpenModule(string? filePath = null)
        {
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath) && TryOpenInWorkspace(filePath))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(filePath) && TryOpenEmptyInWorkspace())
            {
                return;
            }

            OpenStandalone(filePath);
        }

        public static void OpenFromImageView(ColorVision.ImageEditor.EditorContext context)
        {
            string? filePath = context.Config.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show("当前 ImageView 没有关联的文件路径", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            OpenModule(filePath);
        }

        public static bool CanOpenFromImageView(ColorVision.ImageEditor.EditorContext context)
        {
            string? filePath = context.Config.FilePath;
            return !string.IsNullOrWhiteSpace(filePath)
                && File.Exists(filePath)
                && ColorVision.FileIO.CVFileUtil.IsCVCIEFile(filePath);
        }

        private static bool TryOpenInWorkspace(string filePath)
        {
            if (!TryGetWorkspace(out LayoutRoot layoutRoot, out LayoutDocumentPane documentPane))
            {
                return false;
            }

            string contentId = GetContentId(filePath);
            LayoutDocument? existingDocument = WorkspaceManager.FindDocumentById(layoutRoot, contentId);
            if (existingDocument != null)
            {
                SelectDocument(existingDocument);
                return true;
            }

            ConoscopeView view = new ConoscopeView();
            view.OpenConoscope(filePath);

            LayoutDocument layoutDocument = new LayoutDocument
            {
                ContentId = contentId,
                Title = $"Conoscope - {Path.GetFileName(filePath)}",
                Content = view,
                CanClose = true,
                CanFloat = true
            };
            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    Activate(view);
                    WorkspaceManager.OnContentIdSelected(filePath);
                }
            };
            layoutDocument.Closing += (s, e) => view.Dispose();

            documentPane.Children.Add(layoutDocument);
            documentPane.SelectedContentIndex = documentPane.IndexOf(layoutDocument);
            layoutDocument.IsActive = true;
            return true;
        }

        private static bool TryOpenEmptyInWorkspace()
        {
            if (!TryGetWorkspace(out _, out LayoutDocumentPane documentPane))
            {
                return false;
            }

            ConoscopeView view = new ConoscopeView();
            string contentId = $"Conoscope:{Guid.NewGuid():N}";
            LayoutDocument layoutDocument = new LayoutDocument
            {
                ContentId = contentId,
                Title = "Conoscope",
                Content = view,
                CanClose = true,
                CanFloat = true
            };
            layoutDocument.IsActiveChanged += (s, e) =>
            {
                if (layoutDocument.IsActive)
                {
                    Activate(view);
                }
            };
            layoutDocument.Closing += (s, e) => view.Dispose();

            documentPane.Children.Add(layoutDocument);
            documentPane.SelectedContentIndex = documentPane.IndexOf(layoutDocument);
            layoutDocument.IsActive = true;
            return true;
        }

        private static void OpenStandalone(string? filePath)
        {
            ConoscopeWindow window = !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath)
                ? new ConoscopeWindow(filePath)
                : new ConoscopeWindow();

            window.Show();
        }

        private static bool TryGetWorkspace(out LayoutRoot layoutRoot, out LayoutDocumentPane documentPane)
        {
            layoutRoot = null!;
            documentPane = null!;

            try
            {
                layoutRoot = WorkspaceManager.layoutRoot;
                documentPane = WorkspaceManager.LayoutDocumentPane;
                return layoutRoot != null && documentPane != null;
            }
            catch (Exception ex)
            {
                log.Debug($"主工作区尚未就绪: {ex.Message}");
                return false;
            }
        }

        private static string GetContentId(string filePath)
        {
            return "Conoscope:" + Tool.GetMD5(Path.GetFullPath(filePath));
        }

        private static void SelectDocument(LayoutDocument document)
        {
            if (document.Parent is LayoutDocumentPane pane)
            {
                pane.SelectedContentIndex = pane.IndexOf(document);
            }
            else if (document.Parent is LayoutFloatingWindow floatingWindow)
            {
                Window.GetWindow(floatingWindow)?.Activate();
            }

            document.IsActive = true;
        }

        private static void CleanupViews()
        {
            Views.RemoveAll(item => !item.TryGetTarget(out _));
        }
    }
}
