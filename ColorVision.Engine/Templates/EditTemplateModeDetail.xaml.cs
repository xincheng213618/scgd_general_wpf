using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates
{

    public class EditTemplateModeDetailConfig : IConfig
    {
        public static EditTemplateModeDetailConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateModeDetailConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }

    /// <summary>
    /// EditDictionaryMode.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateModeDetail : UserControl
    {
        public EditTemplateModeDetail()
        {
            InitializeComponent();
        }

        public static EditTemplateModeDetailConfig Config => EditTemplateModeDetailConfig.Instance;
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


        public ParamBase Param { get; set; }

        public void SetParam(ParamBase param)
        {
            Param = param;
            this.DataContext = Param;
            if (ListView1.View is GridView gridView)
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is ModDetailModel modDetailModel)
            {
                ModDetailDao.Instance.DeleteById(modDetailModel.Id, false);
                Param.ModDetailModels.Remove(modDetailModel);  
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
