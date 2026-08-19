using System;
using System.IO;
using System.Linq;
using System.Windows;
using ColorVision.ImageEditor;

namespace Conoscope.Core
{
    internal static class ConoscopeModuleService
    {
        public static void OpenModule(string? filePath = null)
        {
            ConoscopeWindow window = GetOrCreateWindow();
            if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
            {
                window.OpenConoscope(filePath);
            }
        }

        public static void OpenFromImageView(EditorContext context)
        {
            string? filePath = context.Config.FilePath;
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                MessageBox.Show(Conoscope.Properties.Resources.MsgImageViewFilePathUnavailable, Conoscope.Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!CanOpenFromImageView(context)) return;
            OpenModule(filePath);
        }

        public static bool CanOpenFromImageView(EditorContext context)
        {
            string? filePath = context.Config.FilePath;
            return !string.IsNullOrWhiteSpace(filePath)
                && File.Exists(filePath)
                && ColorVision.FileIO.CVFileUtil.IsCVCIEFile(filePath)
                && context.Config.GetProperties<int>(ImageViewPropertyKeys.Channel) == 3;
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
    }
}
