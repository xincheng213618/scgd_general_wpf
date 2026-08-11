#pragma warning disable CS0168, CA1863
using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using log4net;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Archive.Dao
{
    /// <summary>
    /// ArchivePage.xaml 的交互逻辑
    /// </summary>
    [Page(nameof(ArchivePage))]
    public partial class ArchivePage : Page, IPage
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ArchivePage));

        public Frame Frame { get; set; }
        public ArchivePage() { }
        public ArchivePage(Frame MainFrame)
        {
            Frame = MainFrame;
            InitializeComponent();
        }
        public ObservableCollection<ArchivedMasterModel> ViewResults { get; set; } = new ObservableCollection<ArchivedMasterModel>();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();

            var MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                SqlSugarClient DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = connStr,
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });

                try
                {
                    foreach (var item in DB.Queryable<ArchivedMasterModel>().ToList())
                    {
                        ViewResults.Add(item);
                    }
                }
                catch (Exception ex) 
                {
                }



            }




        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            listView1.ItemsSource = ViewResults;
            if (listView1.View is GridView gridView)
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
        }
        private void KeyEnter(object sender, KeyEventArgs e)
        {

        }
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewResults.Clear();

            var MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                SqlSugarClient DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = connStr,
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });

                foreach (var item in DB.Queryable<ArchivedMasterModel>().Where(x=>x.Code.Contains(SearchBox.Text)).ToList())
                {
                    ViewResults.Add(item);
                }
            }
        }

        private void Query_Click(object sender, RoutedEventArgs e)
        {
            var MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                SqlSugarClient DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = connStr,
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });

                foreach (var item in DB.Queryable<ArchivedMasterModel>().Where(x => x.Code.Contains(SearchBox.Text)).ToList())
                {
                    ViewResults.Add(item);
                }
            }
        }

        private void listView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();


        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && listView1.View is GridView gridView)
                 GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {
            e.Handled = ViewResults.SortByGridViewColumn<ArchivedMasterModel>(sender, GridViewColumnVisibilitys, Properties.Resources.ResourceManager);

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new ArchiveDetailPage(Frame, ViewResults[listView.SelectedIndex]));
            }
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        private void ArchiveServerConfig_Click(object sender, RoutedEventArgs e)
        {
            GlobleCfgdModel? globleCfgdModel = GlobleCfgdDao.Instance.GetArchDB();
            if (globleCfgdModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ArchiveServerConfigNotFound_Resetting, "ColorVision");
                string sql = "INSERT INTO `cv`.`t_scgd_sys_globle_cfg` (`id`, `code`, `name`, `cfg_type`, `cfg_value`, `is_deleted`, `is_enabled`, `remark`, `tenant_id`) VALUES (3, 'arch_db', '归档服务数据库', 10, '{\\\"Name\\\":null,\\\"Host\\\":\\\"localhost\\\",\\\"Port\\\":3306,\\\"UserName\\\":\\\"cv\\\",\\\"UserPwd\\\":\\\"9p9DMdywXwaTbAXt0oJkUnAb\\\",\\\"Database\\\":\\\"color_vision_arch_2025\\\"}', 0, 1, NULL, 0);\r\n";
                try
                {
                    globleCfgdModel = BatchSqlConsumer.ExecuteAfterCommit(sql, GlobleCfgdDao.Instance.GetArchDB);
                }
                catch (BatchExecuteNonQueryException ex)
                {
                    ShowBatchFailure("初始化归档服务数据库配置", ex);
                    return;
                }
            }
            if (globleCfgdModel == null)
                return;

            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(globleCfgdModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { GlobleCfgdDao.Instance.Save(globleCfgdModel); };
            propertyEditorWindow.ShowDialog();
        }

        private void ArchiveConfiguration_Click(object sender, RoutedEventArgs e)
        {
            const string operation = "更新归档配置数据库结构";
            try
            {
                ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(OpenArchiveConfigurationEditor);
            }
            catch (BatchExecuteNonQueryException ex)
            {
                ShowBatchFailure(operation, ex);
            }
            catch (ArchiveSchemaMigrationException ex)
            {
                ShowSchemaInspectionFailure(operation, ex);
            }
        }

        private void OpenArchiveConfigurationEditor()
        {
            SysConfigRcModel? sysConfigRcModel = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (sysConfigRcModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.RcConfigInfoNotFound, "ColorVision");
                return;
            }
            ConfigArchivedModel? configArchivedModel = ConfigArchivedDao.Instance.GetById(sysConfigRcModel.ArchivedId);
            if (configArchivedModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ArchiveConfigInfoNotFound, "ColorVision");
                return;
            }

            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
            propertyEditorWindow.ShowDialog();
        }

        private static void ShowBatchFailure(string operation, BatchExecuteNonQueryException exception)
        {
            BatchSqlConsumer.ReportUiFailure(log, operation, exception);
        }

        private static void ShowSchemaInspectionFailure(string operation, ArchiveSchemaMigrationException exception)
        {
            log.Error($"{operation}失败。{exception.GetDiagnosticSummary()}");
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                $"{operation}失败，后续操作已停止。\r\n错误标识：{exception.Stage} / {exception.FailureType} ({exception.ErrorCode})。\r\n请检查日志或联系管理员。",
                "ColorVision");
        }

        private void ServiceRegistryCenterConfig_Click(object sender, RoutedEventArgs e)
        {
            SysConfigRcModel? sysConfigRcModel = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (sysConfigRcModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_RCConfigInfoNotFound, RCSetting.Instance.Config.RCName), "ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(sysConfigRcModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { SysConfigRcDao.Instance.Save(sysConfigRcModel); };
            propertyEditorWindow.ShowDialog();
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {

            if (sender is Button button && button.Tag is ArchivedMasterModel archivedMasterModel)
            {
                string SavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Archived");
                if (!Directory.Exists(SavePath))
                    Directory.CreateDirectory(SavePath);

                string Save1Path = Path.Combine(SavePath,archivedMasterModel.Code);
                if (!Directory.Exists(Save1Path)) 
                    Directory.CreateDirectory(Save1Path);
            }
        }

        private void AdvanceQuery_Click(object sender, RoutedEventArgs e)
        {
            var MySqlConfig = GlobleCfgdDao.Instance.GetArchMySqlConfig();
            if (MySqlConfig != null)
            {
                string connStr = $"server={MySqlConfig.Host};port={MySqlConfig.Port};uid={MySqlConfig.UserName};pwd={MySqlConfig.UserPwd};database={MySqlConfig.Database};charset=utf8;Connect Timeout={3};SSL Mode =None;Pooling=true";
                SqlSugarClient DB = new SqlSugarClient(new ConnectionConfig
                {
                    ConnectionString = connStr,
                    DbType = SqlSugar.DbType.MySql,
                    IsAutoCloseConnection = true
                });
            }


        }
    }
}
