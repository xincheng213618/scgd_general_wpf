using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;


namespace ColorVision.Engine.Services.Flow
{
    /// <summary>
    /// EditSpectrum.xaml 的交互逻辑
    /// </summary>
    public partial class EditFlowConfig : Window
    {
        public FlowConfig EditConfig {  get; set; }
        public EditFlowConfig()
        {
            InitializeComponent();
            this.ApplyCaption();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
                if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            EditConfig = FlowConfig.Instance.Clone();
            EditContent.DataContext = EditConfig;
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            EditConfig.CopyTo(FlowConfig.Instance);
            Close();
        }
    }
}
