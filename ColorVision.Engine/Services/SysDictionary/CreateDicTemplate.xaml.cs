﻿using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using ColorVision.Engine.Templates;

namespace ColorVision.Engine.Services.SysDictionary
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateDicTemplate : Window
    {
        public TemplateAlgModParam ITemplate { get; set; }

        public CreateDicTemplate(TemplateAlgModParam template,bool IsImport =false)  
        {
            ITemplate = template;
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.Title += ITemplate.Title;
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateConfig.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code + "." + TemplateConfig.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(TemplateConfig.Instance.DefaultCreateTemplateName),
            ];

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(CreateCode.Text))
            {
                MessageBox.Show("请输入模板名称", "ColorVision");
                return;
            }
            if (ITemplate.ExitsTemplateName(CreateCode.Text))
            {
                MessageBox.Show("已经存在改模板，请修改模板名称", "ColorVision");
                return;
            }

            ITemplate.Create(CreateCode.Text,CreateName.Text);
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