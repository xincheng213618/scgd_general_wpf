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
            //HistoryFolder historyFolder = new HistoryFolder("历史记录");
            //var vhistoryFolder = new VFolder(historyFolder);
            //this.AddChild(vhistoryFolder);

            //List<string> strings = new List<string>() { "MTF", "SFR", "畸变", "灯光检测", "鬼影", "关注点" };

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("MTF");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);

            //    AlgorithmMTFResult MysqlResult = new AlgorithmMTFResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new MTFResult(result).Batch?.DisPlayName ?? new MTFResult(result).Model.Id.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("SFR");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);

            //    AlgorithmSFRResult MysqlResult = new AlgorithmSFRResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new SFRResult(result).Batch?.DisPlayName ?? new SFRResult(result).Model.Id.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("FOV");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);
            //    AlgorithmFOVResult MysqlResult = new AlgorithmFOVResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new FOVResult(result).Batch?.DisPlayName ?? new FOVResult(result).Model.Id.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("Ghost");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);
            //    AlgorithmGhostResult MysqlResult = new AlgorithmGhostResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new GhostResult(result).Batch?.DisPlayName ?? new GhostResult(result).Model.Id.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("Distortion");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);
            //    AlgorithmDistortionResult MysqlResult = new AlgorithmDistortionResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new DistortionResult(result).Batch?.DisPlayName ?? new DistortionResult(result).Model.Id.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}


            foreach (var item in DirectoryInfo.GetDirectories())
            {
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
