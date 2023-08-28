using ColorVision.MySql.DAO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Template
{
    /// <summary>
    /// PG.xaml 的交互逻辑
    /// </summary>
    public partial class PG : UserControl
    {
        public PGParam PGParam { get; set; }
        public PG()
        {
            InitializeComponent();
            this.PGParam = new PGParam();
            this.DataContext = PGParam;
        }
        public PG(PGParam pGParam)
        {
            InitializeComponent();
            this.PGParam = pGParam;
            this.DataContext = PGParam;
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            PGParam.PGtype = 1;
        }

        private void RadioButton_Checked1(object sender, RoutedEventArgs e)
        {
            PGParam.PGtype = 2;
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
