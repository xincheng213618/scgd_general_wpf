using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateTemplate : Window
    {
        public ITemplate ITemplate { get; set; }

        public CreateTemplate(ITemplate template,bool IsImport =false)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title += ITemplate.Title;
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code + "." + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(TemplateSetting.Instance.DefaultCreateTemplateName),
            ];

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
            if (ITemplate.IsUserControl || ITemplate.IsSideHide)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                this.Height = 150;
                //GridProperty.Children.Add(ITemplate.GetUserControl());
            }
            else
            {
                PropertyGrid1.SelectedObject = ITemplate.CreateDefault();
            }
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
            if (ITemplate.ExitsTemplateName(CreateName))
            {
                MessageBox.Show("已经存在改模板，请修改模板名称", "ColorVision");
                return;
            }

            ITemplate.Create(CreateName);
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
