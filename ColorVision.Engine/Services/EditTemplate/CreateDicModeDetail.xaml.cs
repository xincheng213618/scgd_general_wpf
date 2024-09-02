using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
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
            CreateConfig.Id = SysDictionaryModDetailDao.Instance.GetNextAvailableId();
            CreateConfig.IsEnable = true;
            BorderEdit.DataContext  = CreateConfig;

            ComboBoxValueType.ItemsSource = from e1 in Enum.GetValues(typeof(SValueType)).Cast<SValueType>()
                                            select new KeyValuePair<SValueType, string>(e1, e1.ToString());

            
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateConfig.AddressCode = CreateConfig.Id;
            CreateConfig.CreateDate = DateTime.Now;
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
