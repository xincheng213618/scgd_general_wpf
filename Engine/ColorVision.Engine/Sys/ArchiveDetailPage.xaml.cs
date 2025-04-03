using ColorVision.Engine.MySql.ORM;
using ColorVision.UI;

#pragma warning disable CS8602
using ColorVision.UI.Sorts;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.DataHistory.Dao
{ 
    /// <summary>
    /// ArchiveDetailPage.xaml 的交互逻辑
    /// </summary>
    public partial class ArchiveDetailPage : Page
    {
        public Frame Frame { get; set; }
        public ArchivedMasterModel ArchivedMasterModel { get; set; }
        public ArchiveDetailPage(Frame MainFrame , ArchivedMasterModel viewArchiveResult)
        {
            Frame = MainFrame;
            ArchivedMasterModel = viewArchiveResult;
            InitializeComponent();
        }


        public ObservableCollection<ArchivedDetailModel> ViewResults { get; set; } = new ObservableCollection<ArchivedDetailModel>();
        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ViewResults.Clear();
            if (ArchivedMasterModel.Code == null) return;
            foreach (var item in ArchivedDetailDao.Instance.GetAllByParam(new System.Collections.Generic.Dictionary<string, object>() { { "p_guid", ArchivedMasterModel.Code} }))
            {
                ViewResults.Add(item);
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
                Type type = typeof(ArchivedDetailModel);

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

        }

        private void Setting_Click(object sender, RoutedEventArgs e)
        {
            ConfigArchivedModel configArchivedModel = ConfigArchivedDao.Instance.GetById(1);
            if (configArchivedModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"找不到归档配置信息","ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
            propertyEditorWindow.ShowDialog();
        }
    }
}
