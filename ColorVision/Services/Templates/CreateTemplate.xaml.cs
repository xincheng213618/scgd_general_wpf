using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Services.Templates
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTemplate : Window
    {

        public ITemplate ITemplate { get; set; }

        public CreateTemplate(ITemplate template)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            CreateCode.Text = ITemplate.NewCreateFileName("default");
        }


        public string CreateName { get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateName = CreateCode.Text;

            if (string.IsNullOrEmpty(CreateName))
            {
                MessageBox.Show("请输入模板名称", "ColorVision");
                return;
            }
            ITemplate.Create(CreateName);
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
