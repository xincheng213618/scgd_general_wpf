using ColorVision.Common.MVVM;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Media;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using ColorVision.Solution.Explorer;
using System.ComponentModel;
using SolutionFileNode = ColorVision.Solution.Explorer.FileNode;

namespace ColorVision.Engine.Impl.CVFile
{
    internal sealed class CvcieFileActions
    {
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".cvcie",
            ".cvraw",
        };

        public FileInfo FileInfo { get; }
        public RelayCommand ExportCommand { get; set; }
        
        public CvcieFileActions(FileInfo fileInfo)
        {
            FileInfo = fileInfo;
            ExportCommand = new RelayCommand(a => Export(), a => true);
        }

        public static bool Supports(FileInfo fileInfo)
        {
            return fileInfo.Exists && SupportedExtensions.Contains(fileInfo.Extension);
        }

        public void Export()
        {
            new ExportCVCIE(FileInfo.FullName) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    [SolutionMenuContribution(priority: 250)]
    public sealed class FileCVCIEMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.engine.cvcie-file-actions";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is SolutionFileNode fileNode
                && CvcieFileActions.Supports(fileNode.FileInfo);
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var actions = new CvcieFileActions(((SolutionFileNode)context.PrimaryNode).FileInfo);
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "ExportCieImage",
                    Header = Resources.Export,
                    Order = 50,
                    Command = actions.ExportCommand,
                },
            ];
        }
    }
}
