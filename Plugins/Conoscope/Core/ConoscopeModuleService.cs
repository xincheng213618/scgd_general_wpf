using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace Conoscope.Core
{
    internal static class ConoscopeModuleService
    {
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

        public static void RefreshAllReferenceState()
        {
            CleanupViews();
            foreach (ConoscopeView view in Views.Select(item => item.TryGetTarget(out var target) ? target : null).Where(item => item != null)!)
            {
                view.RefreshGlobalReferenceState();
            }
        }

        public static void OpenModule(string? filePath = null)
        {
            ConoscopeWindow window = GetOrCreateWindow();
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                window.OpenConoscope(filePath);
            }
        }

        public static void OpenFromImageView(ColorVision.ImageEditor.EditorContext context)
        {
            string? filePath = context.Config.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(Conoscope.Properties.Resources.MsgImageViewFilePathUnavailable, Conoscope.Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
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

        private static ConoscopeWindow GetOrCreateWindow()
        {
            ConoscopeWindow? window = FindOpenWindow();
            if (window == null)
            {
                window = new ConoscopeWindow();
                window.Show();
            }
            else
            {
                if (window.WindowState == WindowState.Minimized)
                {
                    window.WindowState = WindowState.Normal;
                }

                window.Activate();
            }

            return window;
        }

        private static ConoscopeWindow? FindOpenWindow()
        {
            return Application.Current?.Windows.OfType<ConoscopeWindow>().FirstOrDefault(window => window.IsActive)
                ?? ConoscopeWindow.Instance
                ?? Application.Current?.Windows.OfType<ConoscopeWindow>().FirstOrDefault();
        }

        private static void CleanupViews()
        {
            Views.RemoveAll(item => !item.TryGetTarget(out _));
        }
    }
}
