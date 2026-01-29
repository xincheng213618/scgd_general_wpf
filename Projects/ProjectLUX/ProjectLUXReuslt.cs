using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine;
using ColorVision.Engine.Templates.Flow;
using SqlSugar;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace ProjectLUX
{

    [SugarTable("ARVRReuslt")]
    public class ProjectLUXReuslt : ViewEntity 
    {
        [SqlSugar.SugarColumn(IsIgnore = true)]
        public ContextMenu ContextMenu { get; set; }

        public ProjectLUXReuslt()
        {
            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Command = System.Windows.Input.ApplicationCommands.Copy, Header = "复制" });

            RelayCommand openFolderAndSelectFile = new RelayCommand(a =>
            {
                PlatformHelper.OpenFolderAndSelectFile(FileName);
            }, e => File.Exists(FileName));

            ContextMenu.Items.Add(new MenuItem() { Command = openFolderAndSelectFile, Header = "OpenFolderAndSelectFile" });

            RelayCommand BatchDataHistoryCommand = new RelayCommand(a => BatchDataHistory(),e => BatchId > 0);
            ContextMenu.Items.Add(new MenuItem() { Command = BatchDataHistoryCommand ,Header ="流程结果查询"});

        }

        public void BatchDataHistory()
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            var Batch = Db.Queryable<MeasureBatchModel>().Where(a => a.Id == BatchId).First();
            if (Batch == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "找不到批次号，请检查流程配置", "ColorVision");
                return;
            }
            Frame frame = new Frame();
            MeasureBatchPage batchDataHistory = new MeasureBatchPage(frame, Batch);
            Window window = new Window() { Owner = Application.Current.GetActiveWindow() };
            window.Content = batchDataHistory;
            window.Show();
        }



        public int BatchId { get => _BatchId; set { _BatchId = value; OnPropertyChanged(); } }
        private int _BatchId;

        public string Model { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;

        public string SN { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public FlowStatus FlowStatus { get; set; } = FlowStatus.Ready;

        public bool Result { get; set; } = true;
        public int TestType { get; set; }
        public long RunTime { get; set; }
        public string Msg { get; set; } = string.Empty;

        public DateTime CreateTime { get; set; } = DateTime.Now;


        [SugarColumn(IsNullable = true)]
        public string ViewResultJson { get; set; }

    }


}