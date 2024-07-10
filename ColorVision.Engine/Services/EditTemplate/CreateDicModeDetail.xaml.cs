using ColorVision.Themes;
using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Engine.Services.SysDictionary
{
    /// <summary>
    /// EditTerminal.xaml 的交互逻辑
    /// </summary>
    public partial class CreateDicModeDetail : Window
    {
        DicModParam DicModParam { get; set; }

        public SysDictionaryModDetaiModel CreateConfig { get; set; }

        public CreateDicModeDetail(DicModParam dicModParam)
        {
            DicModParam = dicModParam;
            InitializeComponent();
            this.ApplyCaption();
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
