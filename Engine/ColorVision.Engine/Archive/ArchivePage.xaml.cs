using ColorVision.Database;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using NPOI.SS.Formula.Functions;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
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

                var list = DB.Queryable<T>();
                foreach (var item in DB.Queryable<ArchivedMasterModel>().ToList())
                {
                    ViewResults.Add(item);
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

                var list = DB.Queryable<T>();
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

                var list = DB.Queryable<T>();
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
            if (sender is GridViewColumnHeader gridViewColumnHeader && gridViewColumnHeader.Content != null)
            {
                Type type = typeof(ArchivedMasterModel);

                var properties = type.GetProperties();
                foreach (var property in properties)
                {
                    var attribute = property.GetCustomAttribute<DisplayNameAttribute>();
                    if (attribute != null)
                    {
                        string displayName = attribute.DisplayName;
                        displayName = Properties.Resources.ResourceManager?.GetString(displayName, Thread.CurrentThread.CurrentUICulture) ?? displayName;
                        if (displayName == gridViewColumnHeader.Content.ToString())
                        {
                            var item = GridViewColumnVisibilitys.FirstOrDefault(x => x.ColumnName.ToString() == displayName);
                            if (item != null)
                            {
                                item.IsSortD = !item.IsSortD;
                                ViewResults.SortByProperty(property.Name, item.IsSortD);
                            }
                        }
                    }
                }
            }

        }
        private void listView1_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                Frame.Navigate(new ArchiveDetailPage(Frame, ViewResults[listView.SelectedIndex]));
            }
        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            if (configArchivedModel == null)
            {
                MessageBox.Show("找不到归档配置信息");
            }
            else
            {
                PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow() ,WindowStartupLocation =WindowStartupLocation.CenterOwner };
                propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
                propertyEditorWindow.ShowDialog();
            }
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

                //GenericQuery<ArchivedMasterModel> genericQuery = new GenericQuery<ArchivedMasterModel>(DB, ViewResults);
                //GenericQueryWindow genericQueryWindow = new GenericQueryWindow(genericQuery) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }; ;
                //genericQueryWindow.ShowDialog();
            }


        }
    }
}
