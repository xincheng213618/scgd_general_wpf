using ColorVision.Database;
using ColorVision.Themes;
using SqlSugar;
using System;
using System.Collections.Generic;
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
        ParamModBase Param { get; set; }

        public ModDetailModel CreateConfig { get; set; }

        public CreateModeDetail(ParamModBase dicModParam)
        {
            Param = dicModParam;
            InitializeComponent();
            this.ApplyCaption();
        }

        private List<SysDictionaryModDetaiModel> SysDictionaryModDetaiModels = new List<SysDictionaryModDetaiModel>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Param;
            CreateConfig = new ModDetailModel() { Pid = Param.Id};
            CreateConfig.Pid = Param.Id;
            CreateConfig.SysPid = 500;
            SysDictionaryModDetaiModels = SysDictionaryModDetailDao.Instance.GetAllByPid(Param.ModMaster.Pid);
            BorderEdit.DataContext = CreateConfig;

            if (SysDictionaryModDetaiModels.Count == 0)
            {
                int nid = SysDictionaryModDetailDao.Instance.GetNextAvailableId();
                SysDictionaryModDetaiModel sysDictionaryModDetaiModel = new SysDictionaryModDetaiModel() { Id = nid, AddressCode = nid, PId = Param.Id, Symbol = "default" + nid.ToString(), Name = "default" + nid.ToString(), DefaultValue = "", ValueType = SValueType.String };
                SysDictionaryModDetailDao.Instance.Save(sysDictionaryModDetaiModel);
                SysDictionaryModDetaiModels.Add(sysDictionaryModDetaiModel);
            }

            var values = SysDictionaryModDetaiModels
                .Select(item => new KeyValuePair<int, string>((int)item.AddressCode, item.Symbol?? item.Name ?? item.AddressCode.ToString()))
                .ToList();
            CreateConfig.SysPid = (int)SysDictionaryModDetaiModels[0].AddressCode;
            CreateConfig.ValueA = SysDictionaryModDetaiModels[0].DefaultValue;
            ComboBoxSymbol.ItemsSource = values;
        }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            int id  = Db.Insertable(CreateConfig).ExecuteReturnIdentity();
            CreateConfig.Id = id;
            if (id > 0)
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

        private void ComboBoxSymbol_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox.SelectedIndex > -1)
            {
                CreateConfig.ValueA = SysDictionaryModDetaiModels[comboBox.SelectedIndex].DefaultValue;
            }
        }
    }
}
