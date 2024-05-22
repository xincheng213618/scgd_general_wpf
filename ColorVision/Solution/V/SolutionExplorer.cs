using ColorVision.Common.MVVM;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution.V
{
    public class CVSolution:ViewModelBase
    {
       
    }


    public class SolutionExplorer: VObject
    {
        public DirectoryInfo DirectoryInfo { get; set; }
        public RelayCommand OpenExplorer { get; set; }
        public RelayCommand ClearCacheCommand { get; set; }

        public static SolutionSetting Setting => SolutionSetting.Instance;

        public SolutionExplorer(string FullPath)
        {
            if (File.Exists(FullPath) && FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new(FullPath);
                if (fileInfo !=null)
                {
                    DirectoryInfo = fileInfo.Directory ??new DirectoryInfo(FullPath);
                    Name = Path.GetFileNameWithoutExtension(FullPath);
                    if (DirectoryInfo != null)
                    {
                        DirectoryInfo rootDirectory = DirectoryInfo.Root;
                        DriveInfo = new DriveInfo(rootDirectory.FullName);
                    }
                }
            }
            else if(Directory.Exists(FullPath))
            {
                DirectoryInfo = new DirectoryInfo(FullPath);
                Name = DirectoryInfo.Name;
                DirectoryInfo rootDirectory = DirectoryInfo.Root;
                DriveInfo = new DriveInfo(rootDirectory.FullName);
            }
            GeneralRelayCommand();
            GeneralContextMenu();
            GeneralCVSln();
            IsExpanded = true;
            DriveMonitor();

        }
        public string SolutionCachePath
        {
            get
            {
                // Define the name of the cache folder
                string cacheFolderName = ".SolutionCache";
                // Create the full path for the cache folder
                string cacheFolderPath = Path.Combine(DirectoryInfo.FullName, cacheFolderName);

                // Create the cache folder if it doesn't exist
                if (!Directory.Exists(cacheFolderPath))
                {
                    Directory.CreateDirectory(cacheFolderPath);
                    // Set the folder attributes to hidden
                    File.SetAttributes(cacheFolderPath, FileAttributes.Hidden);
                }
                return cacheFolderPath;
            }
        }

        public void DriveMonitor()
        {
            Task.Run(async () =>
            {
                bool IsMonitor = true;
                while (IsMonitor)
                {
                    await Task.Delay(100000);
                    if (Setting.IsLackWarning)
                    {
                        if (DriveInfo.IsReady)
                        {
                            if (DriveInfo.AvailableFreeSpace < 1024 * 1024 * 1024)
                            {

                                Setting.IsMemoryLackWarning = false;
                                MessageBox.Show("磁盘空间不足");
                            }
                        }
                        else
                        {
                            IsMonitor = false;
                        }
                    }
                }
            });
        }



        public DriveInfo DriveInfo { get; set; }





        public void Refresh()
        {
            VisualChildren.Clear();
            GeneralCVSln();
        }

        public void GeneralRelayCommand()
        {
            OpenExplorer = new RelayCommand(a => 
            System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName));

            ClearCacheCommand = new RelayCommand(a =>
            {
                DirectoryInfo.Delete(true);
                VisualChildren.Clear();
                ///这里之后追加服务的清理
            });
        }

        public void GeneralContextMenu()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new() { Header = "打开工程文件夹", Command = OpenExplorer };
            ContextMenu.Items.Add(menuItem);
            MenuItem menuItem2 = new() { Header = "清除缓存", Command = ClearCacheCommand };
            ContextMenu.Items.Add(menuItem2);
        }



        public void GeneralCVSln()
        {
            foreach (var item in DirectoryInfo.GetDirectories())
            { 
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }

                BaseFolder folder = new(item);
                var vFolder = new VFolder(folder);
                AddChild(vFolder);
                GeneralChild(vFolder, item);
            }
        }

        public static void GeneralChild(VObject vObject,DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                BaseFolder folder = new(item);
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
                else if (item.Extension == ".cvcie")
                {
                    file = new CVcieFile(item);
                }
                else
                {
                    file = new CommonFile(item);
                }
                VFile vFile = new(file);
                vObject.AddChild(vFile);
            }
        }



    }
}
