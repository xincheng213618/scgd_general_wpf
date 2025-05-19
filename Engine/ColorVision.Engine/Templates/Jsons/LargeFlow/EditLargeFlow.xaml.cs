using ColorVision.Engine.Services;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Engine.Templates.Jsons.KB;
using ColorVision.Themes;
using LiveChartsCore.Drawing;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{
    /// <summary>
    /// EditLargeFlow.xaml 的交互逻辑
    /// </summary>
    public partial class EditLargeFlow : Window
    {
        public TJLargeFlowParam TJLargeFlowParam { get; set; }

        public LargeFlowConfig LargeFlowConfig { get; set; }

        public EditLargeFlow(TJLargeFlowParam tJLargeFlowParam)
        {
            TJLargeFlowParam = tJLargeFlowParam;
            LargeFlowConfig = tJLargeFlowParam.LargeFlowConfig;
            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<TemplateModel<FlowParam>> LargeFlowParams { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();
        public ObservableCollection<TemplateModel<FlowParam>> LargeFlowParamAll { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();

        private void Window_Initialized(object sender, EventArgs e)
        {
            var flowNames = new HashSet<string>(LargeFlowConfig.Flows);
            var largeFlowParamsSet = new HashSet<TemplateModel<FlowParam>>();

            foreach (var param in TemplateFlow.Params)
            {
                if (flowNames.Contains(param.Value.Name))
                {
                    largeFlowParamsSet.Add(param);
                    LargeFlowParams.Add(param);
                }
                LargeFlowParamAll.Add(param);
            }

            SeriesExportTreeView1.ItemsSource = LargeFlowParams;
            SeriesExportTreeView2.ItemsSource = LargeFlowParamAll;

        }


        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> deviceService)
            {
                LargeFlowParamAll.Remove(deviceService);
                LargeFlowParamAll.Insert(LargeFlowParamAll.Count, deviceService);
                deviceService.IsSelected = true;
            }
        }

        public static async void GetFocus(TreeView treeView, int index)
        {
            await Task.Delay(1);

            TreeViewItem firstNode = treeView.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;

            // 选中第一个节点
            if (firstNode != null)
            {
                firstNode.IsSelected = true;
                firstNode.Focus();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                int index = LargeFlowParamAll.IndexOf(mQTTDevice);
                if (index - 1 >= 0)
                {
                    LargeFlowParamAll.Remove(mQTTDevice);
                    LargeFlowParamAll.Insert(index - 1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }

            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                int index = LargeFlowParamAll.IndexOf(mQTTDevice);
                if (index + 1 < LargeFlowParamAll.Count)
                {
                    LargeFlowParamAll.Remove(mQTTDevice);
                    LargeFlowParamAll.Insert(index + 1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }


            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParamAll.Remove(mQTTDevice);
                LargeFlowParamAll.Insert(0, mQTTDevice);
                mQTTDevice.IsSelected = true;
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void Button_Click_04(object sender, RoutedEventArgs e)
        {
            foreach (var item in LargeFlowParamAll)
            {
                if (!LargeFlowParams.Contains(item))
                    LargeFlowParams.Add(item);
            }
            LargeFlowParamAll.Clear();
        }

        private void Button_Click_03(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParamAll.Remove(mQTTDevice);
                LargeFlowParams.Add(mQTTDevice);
            }
        }

        private void Button_Click_02(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView1.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParams.Remove(mQTTDevice);
                LargeFlowParamAll.Add(mQTTDevice);
            }
        }

        private void Button_Click_01(object sender, RoutedEventArgs e)
        {
            foreach (var item in LargeFlowParams)
            {

                if (!LargeFlowParamAll.Contains(item))
                    LargeFlowParamAll.Add(item);

            }
            LargeFlowParams.Clear();

        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            LargeFlowConfig.Flows.Clear();
            foreach (var item in LargeFlowParams)
            {
                if (!LargeFlowConfig.Flows.Contains(item.Key))
                    LargeFlowConfig.Flows.Add(item.Key);
            }
            TJLargeFlowParam.JsonValue = JsonConvert.SerializeObject(LargeFlowConfig);
            TemplateJsonDao.Instance.Save(TJLargeFlowParam.TemplateJsonModel);
            Close();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }
    }
}
