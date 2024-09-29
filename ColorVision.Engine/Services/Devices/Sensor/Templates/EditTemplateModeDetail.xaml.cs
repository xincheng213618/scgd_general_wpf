using ColorVision.Engine.Templates;
using ColorVision.UI;
using ColorVision.UI.Sorts;
using MQTTMessageLib.Sensor;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{

    public class EditTemplateSensorConfig : IConfig
    {
        public static EditTemplateSensorConfig Instance => ConfigService.Instance.GetRequiredService<EditTemplateSensorConfig>();
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();
    }

    /// <summary>
    /// EditDictionaryMode.xaml 的交互逻辑
    /// </summary>
    public partial class EditTemplateSensor : UserControl
    {
        public EditTemplateSensor()
        {
            InitializeComponent();
        }

        public static EditTemplateSensorConfig Config => EditTemplateSensorConfig.Instance;
        public ObservableCollection<GridViewColumnVisibility> GridViewColumnVisibilitys { get; set; } = new ObservableCollection<GridViewColumnVisibility>();

        private void UserControl_Initialized(object sender, System.EventArgs e)
        {

        }
        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
        }


        public ParamBase Param { get; set; }

        public void SetParam(ParamBase param)
        {
            Param = param;
            this.DataContext = Param;
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is SensorCommand sensorCommand)
            {
                ModDetailDao.Instance.DeleteById(sensorCommand.Model.Id, false);
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
                comboBox.ItemsSource = Enum.GetValues(typeof(SensorCmdType));

            }
        }
    }
}
