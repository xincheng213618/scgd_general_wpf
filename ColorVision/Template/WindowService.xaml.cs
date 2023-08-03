using ColorVision.Extension;
using ColorVision.MQTT;
using ColorVision.MVVM;
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
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Template
{

    public static class BaseObjectExtensions
    {
        /// <summary>
        /// 得到指定数据类型的祖先节点。
        /// </summary>
        public static T? GetAncestor<T>(this BaseObject This) where T : BaseObject
        {
            if (This is T t)
                return t;

            if (This.Parent == null)
                return null;

            return This.Parent.GetAncestor<T>();
        }
    }


    public class BaseObject : ViewModelBase
    {

        public ContextMenu ContextMenu { get; set; }
        public  ObservableCollection<BaseObject> VisualChildren { get; set; }
        public BaseObject()
        {
            VisualChildren = new ObservableCollection<BaseObject>();
        }
        public BaseObject Parent
        {
            get { return _Parent; }
            set
            {
                _Parent = value;
                NotifyPropertyChanged();
            }
        }
        private BaseObject _Parent;

        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        public virtual void AddChild(BaseObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = this;
            VisualChildren.SortedAdd(baseObject);
        }
        public virtual void RemoveChild(BaseObject baseObject)
        {
            if (baseObject == null) return;
            baseObject.Parent = null;
            VisualChildren.Remove(baseObject);
        }

        public virtual void Save()
        {
        }

    }

    public class MQTTDevice : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        private SysResourceService resourceService = new SysResourceService();

        public MQTTDevice() : base()
        {
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除设备" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                resourceService.DeleteById(SysResourceModel.Id);
            };
            ContextMenu.Items.Add(menuItem);
        }
    }





    public class MQTTService : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        private SysResourceService resourceService = new SysResourceService();

        public ServiceConfig ServiceConfig { get; set; }

        public RelayCommand SaveCommand { get; set; }

        public MQTTService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            Name = sysResourceModel.Name ?? string.Empty;


            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                ServiceConfig ??= new ServiceConfig();
            }
            else
            {
                try
                {
                    ServiceConfig = JsonConvert.DeserializeObject<ServiceConfig>(SysResourceModel.Value) ?? new ServiceConfig();
                }
                catch
                {
                    ServiceConfig = new ServiceConfig();
                }
            }
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                resourceService.DeleteById(SysResourceModel.Id);
            };
            ContextMenu.Items.Add(menuItem);

            SaveCommand = new RelayCommand(a => Save());
        }


        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(ServiceConfig);
            resourceService.Save(SysResourceModel);
        }





    }


    public class MQTTServiceKind : BaseObject
    {
        public SysDictionaryModel SysDictionaryModel { get; set; }
        public MQTTServiceKind() : base()
        {
        }
    }




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
                        mQTTService.Name = item1.Name ?? string.Empty;
                        resourceService.Save(item1);
                        foreach (var item2 in devices)
                        {
                            if (item2.Pid == item1.Id)
                            {    
                                MQTTDevice device = new MQTTDevice();
                                device.Name = item2.Name ?? string.Empty; ;
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
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, ((MQTTService)TextBox_Type.SelectedItem).SysResourceModel.Type, ((MQTTService)TextBox_Type.SelectedItem).SysResourceModel.Id, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                resourceService.Save(sysResource);
                int pkId = sysResource.GetPK();
                if (pkId > 0)
                {
                    SysResourceModel model = resourceService.GetMasterById(pkId);
                    MQTTServices[0].VisualChildren[0].AddChild(new MQTTDevice() { Name = model.Name, SysResourceModel = model });
                }
            }
            else
            {
                SysResourceModel sysResource = new SysResourceModel(TextBox_Name.Text, TextBox_Code.Text, ((MQTTServiceKind)TextBox_Type.SelectedItem).SysDictionaryModel.Value, GlobalSetting.GetInstance().SoftwareConfig.UserConfig.TenantId);
                ServiceConfig ServiceConfig = new ServiceConfig();
                ServiceConfig.SendTopic = SendTopicAdd.Text;
                ServiceConfig.SubscribeTopic = SubscribeTopicAdd.Text;
                sysResource.Value = JsonConvert.SerializeObject(ServiceConfig);

                resourceService.Save(sysResource);
                int pkId = sysResource.GetPK();
                if (pkId > 0)
                {
                    SysResourceModel model = resourceService.GetMasterById(pkId);
                    MQTTServices[0].AddChild(new MQTTService(model));
                }
            }





        }


    }
}
