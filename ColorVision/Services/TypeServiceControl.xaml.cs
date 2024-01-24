using ColorVision.MySql.Service;
using ColorVision.Services.Dao;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services
{
    /// <summary>
    /// TypeServiceControl.xaml 的交互逻辑
    /// </summary>
    public partial class TypeServiceControl : UserControl
    {
        public TypeService ServiceKind { get; set; }

        public ServiceManager ServiceControl { get; set; }
        public TypeServiceControl(TypeService mQTTServiceKind)
        {
            this.ServiceKind = mQTTServiceKind;
            InitializeComponent();
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            ServiceControl = ServiceManager.GetInstance();
            this.DataContext = ServiceKind;
            TextBox_Type.ItemsSource = ServiceControl.TypeServices;
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
            if (!ServicesHelper.IsInvalidPath(TextBox_Name.Text, "服务名称") || !ServicesHelper.IsInvalidPath(TextBox_Code.Text, "服务标识"))
                return;

            if (TextBox_Type.SelectedItem is TypeService serviceKind)
            {

                if (serviceKind.ServicesCodes.Contains(TextBox_Code.Text))
                {
                    MessageBox.Show("服务标识已存在,不允许重复添加");
                    return;
                }
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, serviceKind.SysDictionaryModel.Value, ConfigHandler.GetInstance().SoftwareConfig.UserConfig.TenantId);
               
                TerminalServiceConfig serviceConfig = new TerminalServiceConfig
                {
                    SendTopic = serviceKind.SysDictionaryModel.Code + "/" + "CMD/" + sysResource.Code,
                    SubscribeTopic = serviceKind.SysDictionaryModel.Code + "/" + "STATUS/" + sysResource.Code
                };
            
                sysResource.Value = JsonConvert.SerializeObject(serviceConfig);

                SysResourceService sysResourceService = new SysResourceService();
                sysResourceService.Save(sysResource);

                int pkId = sysResource.GetPK();
                if (pkId > 0 && sysResourceService.GetMasterById(pkId) is SysResourceModel model)
                    serviceKind.AddChild(new TerminalService(model));
            }

        }

        private void ListViewService_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
                if (ServiceKind.VisualChildren[listView.SelectedIndex] is TerminalService serviceTerminal)
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
 