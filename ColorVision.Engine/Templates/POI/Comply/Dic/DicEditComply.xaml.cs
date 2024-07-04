using ColorVision.UI.Sorts;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates.POI.Comply.Dic
{
    /// <summary>
    /// ValidateControl.xaml 的交互逻辑
    /// </summary>
    public partial class DicEditComply : UserControl
    {
        public DicEditComply()
        {
            InitializeComponent();
        }

        public DicComplyParam DicComplyParam { get; set; }

        public void SetParam(DicComplyParam param)
        {
            DicComplyParam = param;
            this.DataContext = DicComplyParam;
        }

        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;  
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is MenuItem menuItem && menuItem.Tag is SysDictionaryModItemValidateModel model)
            //{
            //    ValidateTemplateDetailDao.Instance.DeleteById(validateSingle.Model.Id ,false);
            //    DicComplyParam.ModDetaiModels.Remove(validateSingle.Model);
            //};
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is ContextMenu contextMenu && contextMenu.Items.Count == 0 && ListView1.View is GridView gridView)
                GridViewColumnVisibility.GenContentMenuGridViewColumn(contextMenu, gridView.Columns, GridViewColumnVisibilitys);
        }
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }
}
