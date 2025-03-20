using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{

    public interface ITemplateUserControl
    {
        public void SetParam(object param);
    }

    public partial class TemplateCreate : Window
    {
        public ITemplate ITemplate { get; set; }

        public TemplateCreate(ITemplate template,bool IsImport =false)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title += "模板 " + ITemplate.Title;
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code + "." + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(TemplateSetting.Instance.DefaultCreateTemplateName),
            ];

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
            if (ITemplate.IsSideHide)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                this.Height = 250;

            }
            else if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                this.Height = 250;
                UserControl userControl = ITemplate.CreateUserControl();
                if (userControl is ITemplateUserControl templateUserControl)
                {
                    GridProperty.Children.Add(userControl);
                    templateUserControl.SetParam(ITemplate.CreateDefault());
                    this.Height = userControl.Height +250;
                    this.Width = userControl.Width + 40;
                }
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
