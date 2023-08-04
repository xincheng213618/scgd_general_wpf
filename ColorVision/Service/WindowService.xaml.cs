using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using ColorVision.SettingUp;
using Newtonsoft.Json;
using NPOI.SS.Formula.Functions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Design;
using System.Linq;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    /// <summary>
    /// WindowService.xaml 的交互逻辑
    /// </summary>
    public partial class WindowService : Window
    {
        TemplateControl TemplateControl { get; set; }

        public WindowService()
        {
            InitializeComponent();
        }
        public ObservableCollection<MQTTServiceKind> MQTTServices { get; set; }

        private SysResourceService resourceService = new SysResourceService();
        private SysDictionaryService dictionaryService = new SysDictionaryService();

        private void Window_Initialized(object sender, EventArgs e)
        {
            MQTTServices = new ObservableCollection<MQTTServiceKind>();

            TemplateControl = TemplateControl.GetInstance();


            List<SysResourceModel> Services = resourceService.GetAllServices(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            List<SysResourceModel> devices = resourceService.GetAllDevices(GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
            
            foreach (var item in dictionaryService.GetAllServiceType())
            {
                MQTTServiceKind mQTTServicetype = new MQTTServiceKind();
                mQTTServicetype.Name = item.Name ?? string.Empty;
                mQTTServicetype.SysDictionaryModel = item;
                foreach (var item1 in Services)
                {
                    if (item1.Type == item.Value)
                    {
                        MQTTService mQTTService = new MQTTService(item1);
                        resourceService.Save(item1);
                        foreach (var item2 in devices)
                        {
                            if (item2.Pid == item1.Id)
                            {    
                                MQTTDevice device = new MQTTDevice();
                                device.SysResourceModel = item2;
                                mQTTService.AddChild(device);
                            }
                        }

                        mQTTServicetype.AddChild(mQTTService);
                    }

                }
                MQTTServices.Add(mQTTServicetype);

            }
            TreeView1.ItemsSource = MQTTServices;

            TextBox_Type.ItemsSource = MQTTServices;
        }

        private void TreeView1_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (TreeView1.SelectedItem is MQTTServiceKind MQTTServiceKind)
            {
                type = false;
                StackPanelShow.DataContext = MQTTServiceKind;
                TextBox_Type.ItemsSource = MQTTServices;
                TextBox_Type.SelectedItem = MQTTServiceKind;
                CreateGrid.Visibility = Visibility.Visible;
                MQTTServiceStackPanel.Visibility = Visibility.Collapsed;
            }
            else if (TreeView1.SelectedItem is MQTTService me)
            {
                type = true;
                StackPanelShow.DataContext = me;
                TextBox_Type.ItemsSource = me.Parent.VisualChildren;
                TextBox_Type.SelectedItem = me;
                CreateGrid.Visibility = Visibility.Visible;
                MQTTServiceStackPanel.Visibility = Visibility.Visible;
            }
            else if (TreeView1.SelectedItem is MQTTDevice mQTTDevice)
            {
                StackPanelShow.DataContext = mQTTDevice;
                CreateGrid.Visibility = Visibility.Collapsed;
                MQTTServiceStackPanel.Visibility = Visibility.Collapsed;
            }
        }


        bool type;

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            if (type)
            {
                if (TextBox_Type.SelectedItem is MQTTService mQTTService)
                {
                    SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTService.SysResourceModel.Type, mQTTService.SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                    resourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0)
                    {
                        SysResourceModel model = resourceService.GetMasterById(pkId);
                        mQTTService.AddChild(new MQTTDevice() { Name = model.Name, SysResourceModel = model });
                    }
                }
            }
            else
            {
                if (TextBox_Type.SelectedItem is MQTTServiceKind mQTTServiceKind)
                {
                    SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, mQTTServiceKind.SysDictionaryModel.Value, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                    ServiceConfig ServiceConfig = new ServiceConfig();
                    ServiceConfig.SendTopic = SendTopicAdd.Text;
                    ServiceConfig.SubscribeTopic = SubscribeTopicAdd.Text;
                    sysResource.Value = JsonConvert.SerializeObject(ServiceConfig);

                    resourceService.Save(sysResource);
                    int pkId = sysResource.GetPK();
                    if (pkId > 0)
                    {
                        SysResourceModel model = resourceService.GetMasterById(pkId);
                        mQTTServiceKind.AddChild(new MQTTService(model));
                    }
                }
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            MQTTManager.GetInstance().Reload();
        }
    }
}
