using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using ColorVision.Themes;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateModeDetail : Window
    {
        ParamBase Param { get; set; }

        public ModDetailModel CreateConfig { get; set; }

        public CreateModeDetail(ParamBase dicModParam)
        {
            Param = dicModParam;
            InitializeComponent();
            this.ApplyCaption();
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Param;
            CreateConfig = new ModDetailModel() { };
            CreateConfig.Pid = Param.Id;
            CreateConfig.SysPid = 500;
            BorderEdit.DataContext  = CreateConfig;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int i = ModDetailDao.Instance.Save(CreateConfig);
            if (i > 0)
            {
                Param.ModDetailModels.Add(CreateConfig);
                this.Close();
            }
            else
            {
                MessageBox.Show("添加失败");
            }
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
