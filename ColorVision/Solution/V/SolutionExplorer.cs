using ColorVision.MVVM;
using ColorVision.Services.Algorithm.MySql;
using ColorVision.Services.Algorithm.Result;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using System;
using System.Collections.Generic;
using System.IO;
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


        public SolutionExplorer(string FullPath)
        {
            if (File.Exists(FullPath) && FullPath.EndsWith("cvsln", StringComparison.OrdinalIgnoreCase))
            {
                FileInfo fileInfo = new FileInfo(FullPath);

                this.Name = Path.GetFileNameWithoutExtension(FullPath);
                DirectoryInfo = fileInfo.Directory;
            }
            else if(Directory.Exists(FullPath))
            {
                DirectoryInfo = new DirectoryInfo(FullPath);
                this.Name = DirectoryInfo.Name;
            }


            GeneralRelayCommand();
            GeneralContextMenu();
            GeneralCVSln();
            this.IsExpanded = true;
        }
        public void Refresh()
        {
            this.VisualChildren.Clear();
            GeneralCVSln();
        }

        public void GeneralRelayCommand()
        {
            OpenExplorer = new RelayCommand(a => 
            System.Diagnostics.Process.Start("explorer.exe", DirectoryInfo.FullName));

            ClearCacheCommand = new RelayCommand(a =>
            {
                DirectoryInfo.Delete(true);
                this.VisualChildren.Clear();
                ///这里之后追加服务的清理
            });
        }

        public void GeneralContextMenu()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "打开工程文件夹", Command = OpenExplorer };
            ContextMenu.Items.Add(menuItem);
            MenuItem menuItem2 = new MenuItem() { Header = "清除缓存", Command = ClearCacheCommand };
            ContextMenu.Items.Add(menuItem2);
        }



        public void GeneralCVSln()
        {
            HistoryFolder historyFolder = new HistoryFolder("历史记录");
            var vhistoryFolder = new VFolder(historyFolder);
            this.AddChild(vhistoryFolder);

            List<string> strings = new List<string>() { "MTF", "SFR", "畸变", "灯光检测", "鬼影", "关注点" };

            //if (true)
            //{
            //    HistoryFolder mtfFolder = new HistoryFolder("MTF");
            //    var VFolder = new VFolder(mtfFolder);
            //    vhistoryFolder.AddChild(VFolder);

            //    AlgorithmMTFResult MysqlResult = new AlgorithmMTFResult();
            //    var results = MysqlResult.GetAll();
            //    foreach (var result in results)
            //    {
            //        HistoryFolder historyresult = new HistoryFolder(new MTFResult(result).Batch?.Name ?? new MTFResult(result).Model.ID.ToString());
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
            //        HistoryFolder historyresult = new HistoryFolder(new SFRResult(result).Batch?.Name ?? new SFRResult(result).Model.ID.ToString());
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
            //        HistoryFolder historyresult = new HistoryFolder(new FOVResult(result).Batch?.Name ?? new FOVResult(result).Model.ID.ToString());
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
            //        HistoryFolder historyresult = new HistoryFolder(new GhostResult(result).Batch?.Name ?? new GhostResult(result).Model.ID.ToString());
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
            //        HistoryFolder historyresult = new HistoryFolder(new DistortionResult(result).Batch?.Name ?? new DistortionResult(result).Model.ID.ToString());
            //        var vhistoryFolder2 = new VFolder(historyresult);
            //        VFolder.AddChild(vhistoryFolder2);
            //    }
            //    results.Clear();
            //}


            foreach (var item in DirectoryInfo.GetDirectories())
            {
                BaseFolder folder = new BaseFolder(item);
                var vFolder = new VFolder(folder);
                this.AddChild(vFolder);
                GeneralChild(vFolder, item);
            }
        }

        public static void GeneralChild(VObject vObject,DirectoryInfo directoryInfo)
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
                else if (item.Extension == ".cvcie")
                {
                    file = new CVcieFile(item);
                }
                else
                {
                    file = new CommonFile(item);
                }
                VFile vFile = new VFile(file);
                vObject.AddChild(vFile);
            }
        }



    }
}
