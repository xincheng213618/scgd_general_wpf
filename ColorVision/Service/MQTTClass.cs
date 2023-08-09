using ColorVision.MQTT.Config;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using Newtonsoft.Json;
using System.ComponentModel.Design;
using System.Windows.Controls;

namespace ColorVision.Service
{
    public class MQTTDevice : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }
       
        public MQTTDevice(SysResourceModel device, SysResourceModel service) : base()
        {
            SysResourceModel = device;

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除资源" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                if (SysResourceModel != null)
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);

            };
            ContextMenu.Items.Add(menuItem);

            SubscribeTopic = service.Pcode + "/STATUS/" + service.Code;
            SendTopic = service.Pcode + "/CMD/" + service.Code;
        }

        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }
    }

    public class MQTTDeviceCamera : MQTTDevice
    {
        public CameraConfig CameraConfig { get; set; }
        public RelayCommand SaveCommand { get; set; }

        public MQTTDeviceCamera(SysResourceModel sysResourceModel, SysResourceModel service) : base(sysResourceModel, service)
        {
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                CameraConfig = new CameraConfig();
            }
            else
            {
                try
                {
                    CameraConfig = JsonConvert.DeserializeObject<CameraConfig>(SysResourceModel.Value) ?? new CameraConfig();
                }
                catch
                {
                    CameraConfig = new CameraConfig();
                }
            }

            SaveCommand = new RelayCommand(a => Save());

        }

        //public override string SendTopic { get =>CameraConfig.SendTopic; set { CameraConfig.SendTopic = value;  NotifyPropertyChanged(); } }
        //public override string SubscribeTopic { get =>CameraConfig.SubscribeTopic ; set { CameraConfig.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => CameraConfig.IsAlive;set { CameraConfig.IsAlive = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(CameraConfig);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }



    public class MQTTService : BaseObject
    {
        public SysResourceModel SysResourceModel { get; set; }
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
            ServiceConfig.Name = Name;
            ServiceConfig.SubscribeTopic = SysResourceModel.Pcode + "/STATUS/" + SysResourceModel.Code;
            ServiceConfig.SendTopic = SysResourceModel.Pcode + "/CMD/" + SysResourceModel.Code;
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                if (SysResourceModel != null)
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
            };
            ContextMenu.Items.Add(menuItem);

            SaveCommand = new RelayCommand(a => Save());
        }


        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(ServiceConfig);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
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
