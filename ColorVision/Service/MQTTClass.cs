using ColorVision.MQTT;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.Service
{
    public class MQTTDevice : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        private SysResourceService resourceService = new SysResourceService();
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }
        public MQTTDevice(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除设备" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                if (SysResourceModel != null)
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

        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public MQTTService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
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
                if (SysResourceModel != null)
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
}
