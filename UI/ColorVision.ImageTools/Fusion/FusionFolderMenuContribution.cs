using ColorVision.Common.MVVM;
using ColorVision.Core;
using ColorVision.Solution.Explorer;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.IO;
using System.Windows;

namespace ColorVision.Solution.Fusion
{
    [SolutionMenuContribution(priority: 245)]
    public sealed class FusionFolderMenuContribution : ISolutionMenuContribution
    {
        public string Id => "colorvision.image-tools.fusion";
        public SolutionMenuSelectionPolicy SelectionPolicy => SolutionMenuSelectionPolicy.SingleOnly;

        public bool IsApplicable(SolutionMenuContext context)
        {
            return context.PrimaryNode is FolderNode { DirectoryInfo.Exists: true };
        }

        public IEnumerable<MenuItemMetadata> CreateMenuItems(SolutionMenuContext context)
        {
            var folderNode = (FolderNode)context.PrimaryNode;
            return
            [
                new MenuItemMetadata
                {
                    GuidId = "Fusion",
                    Order = 50,
                    Header = "景深融合(_F)",
                    Command = new RelayCommand(
                        _ => OpenFusion(folderNode.DirectoryInfo),
                        _ => folderNode.DirectoryInfo.Exists),
                },
            ];
        }

        private static void OpenFusion(DirectoryInfo directory)
        {
            var imageFiles = FileFusion.GetFolderFiles(directory.FullName);
            new FusionWindow(imageFiles)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.Show();
        }
    }
}
