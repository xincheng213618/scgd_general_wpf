using ColorVision.Common.MVVM;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace ColorVision.Solution.V
{

    public class SolutionExplorer: VObject
    {
        public DirectoryInfo DirectoryInfo { get; set; }
        public RelayCommand OpenFileInExplorerCommand { get; set; }
        public RelayCommand ClearCacheCommand { get; set; }

        public static SolutionSetting Setting => SolutionSetting.Instance;

        FileSystemWatcher FileSystemWatcher { get; set; }

        public SolutionExplorer(string FullPath)
        {
            if (File.Exists(FullPath) && FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new(FullPath);
                if (fileInfo !=null)
                {
                    DirectoryInfo = fileInfo.Directory ?? new DirectoryInfo(FullPath);
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

            GeneraFileTypes();
            GeneralContextMenu();
            IsExpanded = true;
            DriveMonitor();

            if (DirectoryInfo !=null && DirectoryInfo.Exists)
            {
                FileSystemWatcher = new FileSystemWatcher(DirectoryInfo.FullName) {IncludeSubdirectories =true ,NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite };
                FileSystemWatcher.Created += (s, e) =>
                {
                    var parentDirectory = Directory.GetParent(e.FullPath)?.FullName;
                    foreach (var item in VisualChildren.SelectMany(explorer => explorer.VisualChildren.GetAllVisualChildren()))
                    {
                        if (item is VFolder vFile && vFile.DirectoryInfo.FullName == parentDirectory)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                CreateFile(item, new FileInfo(e.FullPath));
                            });
                        }
                    }
                };
                FileSystemWatcher.Deleted += (s, e) => {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        VisualChildrenEventHandler?.Invoke(this, new EventArgs());
                    });
                };
                FileSystemWatcher.Changed += (s, e) => {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        VisualChildrenEventHandler?.Invoke(this, new EventArgs());
                    });
                };
                FileSystemWatcher.Renamed += (s, e) => { };
                FileSystemWatcher.EnableRaisingEvents = true;
            }


            Task.Run(async () =>
            {
                await Task.Delay(30);
                if(DirectoryInfo!=null && DirectoryInfo.Exists)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        GeneralChild(this, DirectoryInfo);
                    });
                }
            });

        }

        public EventHandler VisualChildrenEventHandler { get; set; }

        public void DriveMonitor()
        {
            Task.Run(async () =>
            {
                bool IsMonitor = true;
                while (IsMonitor)
                {
                    await Task.Delay(100);
                    if (Setting.IsLackWarning)
                    {
                        if (DriveInfo.IsReady)
                        {
                            if (DriveInfo.AvailableFreeSpace < 1024 * 1024 * 1024 )
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

        public void GeneralContextMenu()
        {
            OpenFileInExplorerCommand = new RelayCommand(a => System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName), a => DirectoryInfo.Exists);
            ClearCacheCommand = new RelayCommand(a => { DirectoryInfo.Delete(true); VisualChildren.Clear(); });

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new() { Header = "打开工程文件夹", Command = OpenFileInExplorerCommand };
            ContextMenu.Items.Add(menuItem);
            MenuItem menuItem2 = new() { Header = "清除缓存", Command = ClearCacheCommand };
            ContextMenu.Items.Add(menuItem2);
        }


        public Dictionary<string, Type> FileTypes { get; set; }

        public void GeneraFileTypes()
        {
            FileTypes = new Dictionary<string, Type>();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (typeof(IFileMeta).IsAssignableFrom(type) && !type.IsInterface)
                    {
                        if (Activator.CreateInstance(type) is IFileMeta page)
                        {
                            FileTypes.Add(page.Extension, type);
                        }
                    }
                }
            }
        }

        private static Regex WildcardToRegex(string pattern)
        {
            return new Regex("^" + Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.IgnoreCase);
        }

        public void CreateFile(VObject vObject ,FileInfo fileInfo)
        {
            if (fileInfo.Extension.Contains("cvsln")) return;

            string extension = fileInfo.Extension;
            if (fileInfo.Extension.Contains("lnk"))
            {
                string targetPath = Common.NativeMethods.ShortcutCreator.GetShortcutTargetFile(fileInfo.FullName);
                extension = Path.GetExtension(targetPath);
                fileInfo = new FileInfo(targetPath);
            }
            List<Type> matchingTypes = new List<Type>();
            if (FileTypes.TryGetValue(extension, out Type specificTypes))
            {
                matchingTypes.Add(specificTypes);
            }
            foreach (var key in FileTypes.Keys)
            {
                if (key.Contains(extension))
                    matchingTypes.Add(FileTypes[key]);
            }
            foreach (var key in FileTypes.Keys)
            {
                var subKeys = key.Split('|');
                foreach (var subKey in subKeys)
                {
                    if (WildcardToRegex(subKey).IsMatch(extension))
                    {
                        matchingTypes.Add(FileTypes[key]);
                        break;
                    }
                }
            }
            if (matchingTypes.Count > 0)
            {
                if (Activator.CreateInstance(matchingTypes[0], fileInfo) is IFileMeta file)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        VFile vFile = new VFile(file);
                        vObject.AddChild(vFile);
                    });
                }

            }

        }

        int i = 0;
        public async void GeneralChild(VObject vObject,DirectoryInfo directoryInfo)
        {
            foreach (var item in directoryInfo.GetDirectories())
            {
                if ((item.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }
                BaseFolder folder = new(item);
                var vFolder = new VFolder(folder);
                vObject.AddChild(vFolder);
                i++;
                if (i == 5)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                GeneralChild(vFolder, item);
            }

            foreach (var item in directoryInfo.GetFiles())
            {
                i++;
                if (i == 5)
                {
                    await Task.Delay(100);
                    i = 0;
                }
                CreateFile(vObject, item);
            }
        }



    }
}
