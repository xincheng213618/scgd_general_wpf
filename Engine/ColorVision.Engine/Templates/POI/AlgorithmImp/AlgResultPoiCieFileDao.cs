using ColorVision.Common.MVVM;
using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System.Collections.Generic;
using System.IO;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.POI.AlgorithmImp
{

    [Table("t_scgd_algorithm_result_detail_poi_cie_file", PrimaryKey = "id")]
    public class AlgResultPoiCieFileModel : PKModel, IContextMenu, IViewResult
    {
        public ContextMenu ContextMenu { get; set; }

        public RelayCommand OpenFileCommand { get; set; }
        public RelayCommand OpenFolderCommand { get; set; }

        public static void ExportCSV(IEnumerable<AlgResultPoiCieFileModel> algResultPoiCieFileModels)
        {
            MessageBox1.Show("该功能开发中");
        }

        public AlgResultPoiCieFileModel()
        {
            OpenFileCommand = new RelayCommand(a => OpenFile());
            OpenFolderCommand = new RelayCommand(a => OpenFolder());
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.OpenFile, Command = OpenFileCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.OpenFolder, Command = OpenFileCommand });
        }

        public void OpenFolder()
        {
            if (File.Exists(FileUrl) && Directory.GetParent(FileUrl)?.FullName is string FullName)
            {
                Common.Utilities.PlatformHelper.Open(FullName);
            }
            else
            {
                MessageBox1.Show($"找不到文件:{FileUrl}");
            }
        }

        public void OpenFile()
        {
            if (File.Exists(FileUrl))
            {
                Common.Utilities.PlatformHelper.Open(FileUrl);
            }
            else
            {
                MessageBox1.Show($"找不到文件:{FileUrl}");
            }
        }

        [Column("pid")]
        public int Pid { get; set; }

        [Column("file_name")]
        public string? FileName { get; set; }

        [Column("file_url")]
        public string? FileUrl { get; set; }

        [Column("file_type")]
        public string? FileType { get; set; }
    }


    public class AlgResultPoiCieFileDao : BaseTableDao<AlgResultPoiCieFileModel>
    {
        public static AlgResultPoiCieFileDao Instance { get; set; } = new AlgResultPoiCieFileDao();

        public AlgResultPoiCieFileDao() : base("t_scgd_algorithm_result_detail_poi_cie_file")
        {
        }
    }
}
