using ColorVision.Database;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Sensor;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{

    public class EditTemplateSensorConfig : ViewModelBase, IConfig
    {
        public static EditTemplateSensorConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateSensorConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        public bool IsCommandPreviewVisible { get => _IsCommandPreviewVisible; set => SetProperty(ref _IsCommandPreviewVisible, value); }
        private bool _IsCommandPreviewVisible = true;

        public bool UseControlNamesInBracketText { get => _UseControlNamesInBracketText; set => SetProperty(ref _UseControlNamesInBracketText, value); }
        private bool _UseControlNamesInBracketText;
    }

    /// <summary>
    /// EditDictionaryMode.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateSensor : UserControl
    {
        public EditTemplateSensor()
        {
            InitializeComponent();
            Loaded += EditTemplateSensor_Loaded;
            Unloaded += EditTemplateSensor_Unloaded;
        }

        public static EditTemplateSensorConfig Config => EditTemplateSensorConfig.Instance;

        private void EditTemplateSensor_Loaded(object sender, RoutedEventArgs e)
        {
            Config.PropertyChanged -= Config_PropertyChanged;
            Config.PropertyChanged += Config_PropertyChanged;
        }

        private void EditTemplateSensor_Unloaded(object sender, RoutedEventArgs e)
        {
            Config.PropertyChanged -= Config_PropertyChanged;
        }

        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(EditTemplateSensorConfig.UseControlNamesInBracketText))
            {
                return;
            }

            if (Param is not SensorParam sensorParam)
            {
                return;
            }

            foreach (var command in sensorParam.SensorCommands)
            {
                command.RefreshPreviewProperties();
            }
        }
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
        }


        public ParamModBase Param { get; set; }

        public void SetParam(ParamModBase param)
        {
            Param = param;
            if (param is SensorParam sensorParam)
            {
                TemplateSensor.EnsureDefaultCommandDefinition(sensorParam.ModMaster.Pid);
            }
            this.DataContext = Param;
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SensorCommand sensorCommand)
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == sensorCommand.Model.Id).ExecuteCommand();
                Param.ModDetailModels.Remove(sensorCommand.Model);
            }
        }


        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void ComboBoxType_Initialized(object sender, System.EventArgs e)
        {
            if (sender is ComboBox comboBox)
            {
                comboBox.ItemsSource = Enum.GetValues<SensorCmdType>();

            }
        }
    }
}
