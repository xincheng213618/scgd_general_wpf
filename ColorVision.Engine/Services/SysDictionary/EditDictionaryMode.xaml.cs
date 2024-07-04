using ColorVision.Engine.Templates.POI.Comply;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.SysDictionary
{
    /// <summary>
    /// EditDictionaryMode.xaml 的交互逻辑
    /// </summary>
    public partial class EditDictionaryMode : UserControl
    {
        public EditDictionaryMode()
        {
            InitializeComponent();
        }

        public DicModParam Param { get; set; }

        public void SetParam(DicModParam param)
        {
            Param = param;
            this.DataContext = Param;
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
