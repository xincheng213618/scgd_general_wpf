﻿using ColorVision.RC;
using ColorVision.Services.Dao;
using ColorVision.Services.Terminal;
using ColorVision.Settings;
using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Services.Type
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
               
                DBTerminalServiceConfig dbCfg = new DBTerminalServiceConfig {  HeartbeatTime = 5000,};
                sysResource.Value = JsonConvert.SerializeObject(dbCfg);
                
                VSysResourceDao resourceDao = new VSysResourceDao();
                resourceDao.Save(sysResource);

                int pkId = sysResource.PKId;
                if (pkId > 0 && resourceDao.GetById(pkId) is SysResourceModel model)
                    serviceKind.AddChild(new TerminalService(model));
                MQTTRCService.GetInstance().RestartServices(serviceKind.ServiceTypes.ToString());
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
 