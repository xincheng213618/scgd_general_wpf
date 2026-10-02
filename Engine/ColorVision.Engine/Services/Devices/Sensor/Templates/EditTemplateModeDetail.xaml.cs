using ColorVision.Database;
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Sensor;
using SqlSugar;
using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
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
    /// 传感器模板明细编辑窗口的交互逻辑
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
            if (Param is SensorParam sensorParam)
            {
                sensorParam.SensorCommands.CollectionChanged -= Commands_CollectionChanged;
                sensorParam.SensorCommands.CollectionChanged += Commands_CollectionChanged;
            }
            RefreshCommandSelection(CommandForm.DataContext as SensorCommand);
        }

        private void EditTemplateSensor_Unloaded(object sender, RoutedEventArgs e)
        {
            Config.PropertyChanged -= Config_PropertyChanged;
            if (Param is SensorParam sensorParam)
                sensorParam.SensorCommands.CollectionChanged -= Commands_CollectionChanged;
        }

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
        public ParamModBase Param { get; set; }

        public void SetParam(ParamModBase param)
        {
            if (Param is SensorParam previous)
                previous.SensorCommands.CollectionChanged -= Commands_CollectionChanged;
            Param = param;
            if (param is SensorParam sensorParam)
            {
                if (sensorParam.ModMaster.Pid > 0)
                    TemplateSensor.EnsureDefaultCommandDefinition(sensorParam.ModMaster.Pid);
                sensorParam.SensorCommands.CollectionChanged += Commands_CollectionChanged;
            }
            this.DataContext = Param;
            RefreshCommandSelection(null, 0);
        }

        private void Commands_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            var preferred = e.NewItems?.OfType<SensorCommand>().LastOrDefault()
                ?? CommandForm.DataContext as SensorCommand;
            RefreshCommandSelection(preferred, CommandSelector.SelectedIndex);
        }

        private void RefreshCommandSelection(SensorCommand? preferred, int fallbackIndex = 0)
        {
            var commands = (Param as SensorParam)?.SensorCommands;
            int count = commands?.Count ?? 0;
            int index = preferred == null ? -1 : commands!.IndexOf(preferred);
            if (index < 0) index = Math.Clamp(fallbackIndex, 0, Math.Max(0, count - 1));
            CommandSelector.ItemsSource = Enumerable.Range(1, count).Select(number => $"#{number}").ToArray();
            CommandSelector.SelectedIndex = count == 0 ? -1 : index;
            CommandSelectionPanel.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
            DeleteCommandButton.IsEnabled = count > 0;
            CommandForm.Visibility = count > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void CommandSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var commands = (Param as SensorParam)?.SensorCommands;
            int index = CommandSelector.SelectedIndex;
            CommandForm.DataContext = commands != null && index >= 0 && index < commands.Count ? commands[index] : null;
            CommandPosition.Text = commands != null && index >= 0 ? $"{index + 1} / {commands.Count}" : string.Empty;
        }

        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (CommandForm.DataContext is SensorCommand sensorCommand)
            {
                if (Param.Id > 0 && sensorCommand.Model.Id > 0)
                {
                    using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                    Db.Deleteable<ModDetailModel>().Where(x => x.Id == sensorCommand.Model.Id && x.Pid == Param.Id).ExecuteCommand();
                }
                Param.ModDetailModels.Remove(sensorCommand.Model);
            }
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
