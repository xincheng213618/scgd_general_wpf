using ColorVision.Common.MVVM;
using ColorVision.Engine.Media;
using ColorVision.Engine.ToolPlugins.ThirdPartyApps;
using ColorVision.ImageEditor;
using ColorVision.FileIO;
using ColorVision.UI.Menus;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;


namespace WindowsServicePlugin.Tools
{
    public static class PathHelper
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern int GetShortPathName(
            string lpszLongPath,
            System.Text.StringBuilder lpszShortPath,
            int cchBuffer);

        public static string GetShortPath(string longPath)
        {
            var shortPath = new System.Text.StringBuilder(260);
            GetShortPathName(longPath, shortPath, shortPath.Capacity);
            return shortPath.ToString();
        }
    }



    public record class ImageViewExTension(EditorContext EditorContext) : IIEditorToolContextMenu
    {

        public List<MenuItemMetadata> GetContextMenuItems()
        {
            List<MenuItemMetadata> values = new List<MenuItemMetadata>();

            if (!File.Exists(ExternalToolsConfig.Instance.ImageJPath)) return values;

            RelayCommand relayCommand = new RelayCommand(a =>
            {
                string shortFilePath = string.Empty;
                if (CVFileUtil.IsCIEFile(EditorContext.Config.FilePath))
                {
                    VExportCIE vExportCIE = new VExportCIE(EditorContext.Config.FilePath);
                    VExportCIE.SaveToTif(vExportCIE);
                    shortFilePath = PathHelper.GetShortPath(vExportCIE.CoverFilePath);
                }
                else
                {
                    shortFilePath = PathHelper.GetShortPath(EditorContext.Config.FilePath);
                }

                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = ExternalToolsConfig.Instance.ImageJPath,
                    Arguments = $"\"{shortFilePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process.Start(startInfo);
            });

            if (File.Exists(EditorContext.Config.FilePath))
            {
                MenuItemMetadata menuItemMetadata = new MenuItemMetadata() { GuidId = "ImageJ", Order = 500, Header = "通过ImageJ打开", Command = relayCommand };
                values.Add(menuItemMetadata);
            }
            return values;
        }
    }

}
