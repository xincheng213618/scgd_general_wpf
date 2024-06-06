using ColorVision.Common.Extension;
using ColorVision.Common.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Services.SysDictionary
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateModeDetail : Window
    {
        DicModParam DicModParam { get; set; }

        public SysDictionaryModDetaiModel CreateConfig { get; set; }

        public CreateModeDetail(DicModParam dicModParam)
        {
            DicModParam = dicModParam;
            InitializeComponent();
        }


        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = DicModParam;
            CreateConfig = new SysDictionaryModDetaiModel() {  PId = DicModParam.Id};
            BorderEdit.DataContext  = CreateConfig;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            int i = SysDictionaryModDetailDao.Instance.Save(CreateConfig);
            if (i > 0)
            {
                DicModParam.ModDetaiModels.Add(CreateConfig);
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
