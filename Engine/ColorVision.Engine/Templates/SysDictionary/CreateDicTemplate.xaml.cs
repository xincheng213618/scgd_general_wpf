using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Menus;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateDicTemplate : Window
    {
        public ITemplate ITemplate { get; set; }

        public CreateDicTemplate(ITemplate template,bool IsImport =false)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title += ITemplate.Title;
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code + "." + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(TemplateSetting.Instance.DefaultCreateTemplateName),
            ];

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CreateCode.Text))
            {
                MessageBox.Show(ColorVision.Engine.Properties.Resources.InputTemplateName, "ColorVision");
                return;
            }

            if (TemplateControl.ExitsTemplateName(CreateCode.Text))
            {
                MessageBox.Show(ColorVision.Engine.Properties.Resources.TemplateExists_PleaseRename, "ColorVision");
                return;
            }

            ITemplate.Create(CreateCode.Text,CreateName.Text);
            MenuManager.GetInstance().RefreshMenuItemsByGuid(nameof(MenuTemplate));
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
