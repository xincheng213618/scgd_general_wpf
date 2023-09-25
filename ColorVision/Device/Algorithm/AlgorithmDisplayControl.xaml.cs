using ColorVision.Device.Algorithm;
using ColorVision.MQTT.Service;
using ColorVision.MySql.Service;
using ColorVision.Template;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Device.POI
{
    /// <summary>
    /// AlgorithmDisplayControl.xaml 的交互逻辑
    /// </summary>
    public partial class AlgorithmDisplayControl : UserControl
    {
        public DeviceAlgorithm Device { get; set; }

        public AlgorithmService Service { get => Device.Service; }

        public AlgorithmView View { get => Device.View; }


        public AlgorithmDisplayControl(DeviceAlgorithm device)
        {
            Device = device;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ComboxPoiTemplate.ItemsSource = TemplateControl.GetInstance().PoiParams;
            ComboxPoiTemplate.SelectedIndex = 0;
            ViewGridManager.GetInstance().AddView(Device.View);
            ViewGridManager.GetInstance().ViewMaxChangedEvent += (e) =>
            {
                List<KeyValuePair<string, int>> KeyValues = new List<KeyValuePair<string, int>>();
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowSingle, -2));
                KeyValues.Add(new KeyValuePair<string, int>(Properties.Resource.WindowHidden, -1));
                for (int i = 0; i < e; i++)
                {
                    KeyValues.Add(new KeyValuePair<string, int>((i + 1).ToString(), i));
                }
                ComboxView.ItemsSource = KeyValues;
                ComboxView.SelectedValue = View.View.ViewIndex;
            };
            View.View.ViewIndexChangedEvent += (e1, e2) =>
            {
                ComboxView.SelectedIndex = e2 + 2;
            };
            ComboxView.SelectionChanged += (s, e) =>
            {
                if (ComboxView.SelectedItem is KeyValuePair<string, int> KeyValue)
                {
                    View.View.ViewIndex = KeyValue.Value;
                    ViewGridManager.GetInstance().SetViewIndex(View, KeyValue.Value);
                }
            };
            View.View.ViewIndex = -1;


        }

        private void PoiClick(object sender, RoutedEventArgs e)
        {
            if (ComboxPoiTemplate.SelectedValue is PoiParam poiParam)
            {
                string sn = DateTime.Now.ToString("yyyyMMdd'T'HHmmss.fffffff");
                var model = ServiceControl.GetInstance().GetResultBatch(sn);
                Service.GetData(poiParam.ID,10);
            }
        }

        private void Algorithm_INI(object sender, RoutedEventArgs e)
        {
            Service.Init();
        }

        private void Algorithm_GET(object sender, RoutedEventArgs e)
        {
            Service.GetAllSnID();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var a = new ResultService().PoiSelectByBatchID(10);
            Device.View.PoiDataDraw(a);
        }

    }
}
