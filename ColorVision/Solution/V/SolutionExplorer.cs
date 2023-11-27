using ColorVision.Device;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.Services.Algorithm;
using ColorVision.Solution.V.Files;
using ColorVision.Solution.V.Folders;
using cvColorVision;
using Org.BouncyCastle.Asn1.X509;
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
        public ContextMenu ContextMenu { get; set; }
        public DirectoryInfo DirectoryInfo { get; set; }

        public RelayCommand OpenExplorer { get; set; }


        public SolutionExplorer(string FullPath)
        {
            DirectoryInfo = new DirectoryInfo(FullPath);
            this.Name = DirectoryInfo.Name;
            GeneralRelayCommand();
            GeneralContextMenu();
            GeneralCVSln();
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



        public void GeneralCVSln()
        {
            HistoryFolder historyFolder = new HistoryFolder("历史记录");
            var vhistoryFolder = new VFolder(historyFolder);
            this.AddChild(vhistoryFolder);

            List<string> strings = new List<string>() { "MTF", "SFR", "畸变", "灯光检测", "鬼影", "关注点" };
            foreach (var item in strings)
            {
                HistoryFolder historyFolder1 = new HistoryFolder(item);
                var vhistoryFolder1 = new VFolder(historyFolder1);
                vhistoryFolder.AddChild(vhistoryFolder1);

                AlgorithmMTFResult AlgorithmMTFResult = new AlgorithmMTFResult();
                var results = AlgorithmMTFResult.GetAll();
                foreach (var result in results)
                {
                    HistoryFolder historyresult = new HistoryFolder(new MTFResult(result).Batch.Name);
                    var vhistoryFolder2 = new VFolder(historyresult);
                    vhistoryFolder1.AddChild(vhistoryFolder2);
                }
                results.Clear();

            }

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
                    file = new ImageFile(item);
                }
                VFile vFile = new VFile(file);
                vObject.AddChild(vFile);
            }
        }



    }
}
