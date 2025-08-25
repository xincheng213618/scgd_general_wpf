using ColorVision.Database;
using ColorVision.Engine.Services.Dao;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.SysDictionary
{

    public class EditDictionaryModeConfig : IConfig
    {
        public static EditDictionaryModeConfig Instance => ConfigService.Instance.GetRequiredService<EditDictionaryModeConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }

    /// <summary>
    /// EditDictionaryMode.xaml 的交互逻辑
    /// </summary>
    public partial class EditDictionaryMode : UserControl
    {
        public EditDictionaryMode()
        {
            InitializeComponent();
        }
        public static EditDictionaryModeConfig Config => EditDictionaryModeConfig.Instance;
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {
            if (ListView1.View is GridView gridView)
            {
                GridViewColumnVisibility.AddGridViewColumn(gridView.Columns, GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys.CopyToGridView(GridViewColumnVisibilitys);
                Config.GridViewColumnVisibilitys = GridViewColumnVisibilitys;
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
            }
        }
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }


        public DicModParam Param { get; set; }

        public void SetParam(DicModParam param)
        {
            Param = param;
            this.DataContext = Param;
            if (ListView1.View is GridView gridView)
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);

        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SysDictionaryModDetaiModel SysDictionaryModDetaiModel)
            {
                MySqlControl.GetInstance().DB.Deleteable<SysResourceModel>().Where(it => it.Id == SysDictionaryModDetaiModel.Id).ExecuteCommand();
                Param.ModDetaiModels.Remove(SysDictionaryModDetaiModel);
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }


    }
}
