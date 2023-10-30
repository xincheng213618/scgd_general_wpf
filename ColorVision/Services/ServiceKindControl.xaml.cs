using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services
{
    /// <summary>
    /// ServiceKindControl.xaml 的交互逻辑
    /// </summary>
    public partial class ServiceKindControl : UserControl
    {
        public ServiceKind ServiceKind { get; set; }

        public ServiceManager ServiceControl { get; set; }
        public ServiceKindControl(ServiceKind mQTTServiceKind)
        {
            this.ServiceKind = mQTTServiceKind;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceManager.GetInstance();
            this.DataContext = ServiceKind;
            TextBox_Type.ItemsSource = ServiceControl.MQTTServices;
            TextBox_Type.SelectedItem = ServiceKind;

            if (ServiceKind.VisualChildren.Count == 0)
                ListViewService.Visibility = Visibility.Collapsed;
            ListViewService.ItemsSource = ServiceKind.VisualChildren;

            ServiceKind.VisualChildren.CollectionChanged += (s, e) =>
            {
                if (ServiceKind.VisualChildren.Count == 0)
                {
                    ListViewService.Visibility = Visibility.Collapsed;
                }
                else
                {
                    ListViewService.Visibility = Visibility.Visible;
                }
            };
        }


        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (!MQTT.Util.IsInvalidPath(TextBox_Name.Text, "服务名称") || !MQTT.Util.IsInvalidPath(TextBox_Code.Text, "服务标识"))
                return;

            if (TextBox_Type.SelectedItem is ServiceKind serviceKind)
            {

                if (serviceKind.ServicesCodes.Contains(TextBox_Code.Text))
                {
                    MessageBox.Show("服务标识已存在,不允许重复添加");
                    return;
                }
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, serviceKind.SysDictionaryModel.Value, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
               
                BaseServiceConfig serviceConfig = new BaseServiceConfig
                {
                    SendTopic = serviceKind.SysDictionaryModel.Code + "/" + "CMD/" + sysResource.Code,
                    SubscribeTopic = serviceKind.SysDictionaryModel.Code + "/" + "STATUS/" + sysResource.Code
                };
            
                sysResource.Value = JsonConvert.SerializeObject(serviceConfig);

                SysResourceService sysResourceService = new SysResourceService();
                sysResourceService.Save(sysResource);

                int pkId = sysResource.GetPK();
                if (pkId > 0 && sysResourceService.GetMasterById(pkId) is SysResourceModel model)
                    serviceKind.AddChild(new ServiceTerminal(model));
            }

        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (ServiceKind.VisualChildren[listView.SelectedIndex] is ServiceTerminal serviceTerminal)
                {
                    if (this.Parent is Grid grid)
                    {
                        grid.Children.Clear();
                        grid.Children.Add(serviceTerminal.GenDeviceControl());
                    }
                    
                }
            }
        }
    }
}
 