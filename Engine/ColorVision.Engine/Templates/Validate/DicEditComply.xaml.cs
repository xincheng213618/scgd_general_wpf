using ColorVision.UI;
using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.Validate
{
    public class DicEditComplyConfig : IConfig
    {
        public static DicEditComplyConfig Instance => ConfigService.Instance.GetRequiredService<DicEditComplyConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }

    /// <summary>
    /// ValidateControl.xaml 的交互逻辑
    /// </summary>
    public partial class DicEditComply : UserControl
    {
        public DicEditComply()
        {
            InitializeComponent();
        }
        public static DicEditComplyConfig Config => DicEditComplyConfig.Instance;
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
        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        public DicComplyParam DicComplyParam { get; set; }

        public void SetParam(DicComplyParam param)
        {
            DicComplyParam = param;
            this.DataContext = DicComplyParam;
            if (ListView1.View is GridView gridView)
                GridViewColumnVisibility.AdjustGridViewColumnAuto(gridView.Columns, GridViewColumnVisibilitys);
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;  
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is MenuItem menuItem && menuItem.ValidateTId is SysDictionaryModItemValidateModel model)
            //{
            //    ValidateTemplateDetailDao.Instance.DeleteById(validateSingle.Model.Id ,false);
            //    DicComplyParam.ModDetaiModels.Remove(validateSingle.Model);
            //};
        }






    }
}
