using ColorVision.Device;
using ColorVision.Device.Camera;
using ColorVision.MQTT;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System.Windows.Controls;

namespace ColorVision.Services
{
    public class BaseMQTTService : BaseObject
    {
        public virtual UserControl GenDeviceControl()
        {
            throw new System.NotImplementedException();
        }
    }

    public class MQTTService : BaseMQTTService
    {
        public SysResourceModel SysResourceModel { get; set; }
        public BaseServiceConfig Config { get; set; }
        public BaseService BaseService { get; set; }

        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public MQTTService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config ??= new BaseServiceConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<BaseServiceConfig>(SysResourceModel.Value) ?? new BaseServiceConfig();
                }
                catch
                {
                    Config = new BaseServiceConfig();
                }
            }
            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = Name;
            Config.SubscribeTopic = SysResourceModel.TypeCode + "/STATUS/" + SysResourceModel.Code;
            Config.SendTopic = SysResourceModel.TypeCode + "/CMD/" + SysResourceModel.Code;



            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                Parent.RemoveChild(this);
                if (SysResourceModel != null)
                {
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
                    ServiceControl.GetInstance().ResourceService.DeleteAllByPid(SysResourceModel.Id);
                }
            };
            ContextMenu.Items.Add(menuItem);

            BaseService = new BaseService();


        }

        public ServiceType Type { get => (ServiceType)SysResourceModel.Type; }


        public override UserControl GenDeviceControl() => new MQTTServiceControl(this);

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }
}
