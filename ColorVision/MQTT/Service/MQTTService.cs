using ColorVision.Device;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.MQTT.Service
{
    public class MQTTService : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        public ServiceConfig ServiceConfig { get; set; }
        public HeartbeatService HeartbeatService { get; set; }

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
            ServiceConfig.Name = Name;
            ServiceConfig.SubscribeTopic = SysResourceModel.TypeCode + "/STATUS/" + SysResourceModel.Code;
            ServiceConfig.SendTopic = SysResourceModel.TypeCode + "/CMD/" + SysResourceModel.Code;
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                Parent.RemoveChild(this);
                if (SysResourceModel != null)
                {
                    //先标记自己为删除状态，在将自己的子节点标记为删除状态
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
                    ServiceControl.GetInstance().ResourceService.DeleteAllByPid(SysResourceModel.Id);
                }
            };
            ContextMenu.Items.Add(menuItem);

            HeartbeatService = new HeartbeatService(ServiceConfig);
        }

        public DeviceType Type { get => (DeviceType)SysResourceModel.Type; }


        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(ServiceConfig);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }
}
