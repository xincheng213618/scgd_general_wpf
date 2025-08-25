using ColorVision.Engine.Templates.Validate;
using ColorVision.UI;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class EditTemplateBuzProductConfig : IConfig
    {
        public static EditTemplateBuzProductConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateBuzProductConfig>();

        public double Width { get; set; } = double.NaN;
    }

    /// <summary>
    /// EditTemplateBuzProduct.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateBuzProduct : UserControl, ITemplateUserControl
    {
        public EditTemplateBuzProduct()
        {
            InitializeComponent();
            this.Width = EditTemplateBuzProductConfig.Instance.Width;
            this.SizeChanged += (s, e) =>
            {
                EditTemplateBuzProductConfig.Instance.Width = this.ActualWidth;
            };
        }
        public void SetParam(object param)
        {
            this.DataContext = param;
        }

        private void ComboBoxValidateCIE_Initialized(object sender, EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = TemplateComplyParam.CIEParams.SelectMany(kvp => kvp.Value) .ToList();
            }
        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }
    }
}
