using ColorVision.Device;
using ColorVision.MVVM;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Solution.V
{
    public class SolutionExplorer: VObject
    {
        public ContextMenu ContextMenu { get; set; }
        public DirectoryInfo DirectoryInfo { get; set; }

        public RelayCommand OpenExplorer { get; set; }


        public SolutionExplorer(string FullPath)
        {
            DirectoryInfo = new DirectoryInfo(FullPath);
            this.Name = DirectoryInfo.Name;
            GeneralRelayCommand();
            GeneralContextMenu();

            
            GeneralChild(this,DirectoryInfo);
            this.IsExpanded = true;
        }

        public void GeneralRelayCommand()
        {
            OpenExplorer = new RelayCommand(a => 
            System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName));
        }

        public void GeneralContextMenu()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "打开文件夹", Command = OpenExplorer };
            ContextMenu.Items.Add(menuItem);
        }

        public void GeneralChild(VObject vObject,DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                BaseFolder folder = new BaseFolder(item);
                var vFolder = new VFolder(folder);
                vObject.AddChild(vFolder);
                GeneralChild(vFolder, item);
            }
            foreach (var item in directoryInfo.GetFiles())
            {
                IFile file;
                if (item.Extension ==".stn")
                {
                    file = new FlowFile(item);
                }
                else
                {
                    file = new ImageFile(item);
                }
                VFile vFile = new VFile(file);
                vObject.AddChild(vFile);
            }
        }



    }
}
