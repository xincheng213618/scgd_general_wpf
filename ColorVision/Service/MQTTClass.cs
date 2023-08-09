using ColorVision.MQTT;
using ColorVision.MQTT.Config;
using ColorVision.MVVM;
using ColorVision.MySql.DAO;
using ColorVision.MySql.Service;
using Newtonsoft.Json;
using System;
using System.ComponentModel.Design;
using System.Windows.Controls;

namespace ColorVision.Service
{
    public class MQTTDevice : BaseObject
    {

        public SysResourceModel SysResourceModel { get; set; }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }
       
        public MQTTDevice(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除资源" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
                if (SysResourceModel != null)
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);

            };
            ContextMenu.Items.Add(menuItem);
        }
        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }


    }

    public class MQTTDevicePG : MQTTDevice
    {
        public PGConfig Config { get; set; }
        public MQTTDevicePG(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config = new PGConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<PGConfig>(SysResourceModel.Value) ?? new PGConfig();
                }
                catch
                {
                    Config = new PGConfig();
                }
            }
        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }


    }
    
    public class MQTTDeviceSpectrum : MQTTDevice
    {
        public SpectrumConfig Config { get; set; }

        public MQTTDeviceSpectrum(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config = new SpectrumConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<SpectrumConfig>(SysResourceModel.Value) ?? new SpectrumConfig();
                }
                catch
                {
                    Config = new SpectrumConfig();
                }
            }
        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }


    public class MQTTDeviceCamera : MQTTDevice
    {
        public CameraConfig Config { get; set; }

        public MQTTDeviceCamera(SysResourceModel sysResourceModel) : base(sysResourceModel)
        {
            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config = new CameraConfig();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<CameraConfig>(SysResourceModel.Value) ?? new CameraConfig();
                }
                catch
                {
                    Config = new CameraConfig();
                }
            }
        }


        public override string SendTopic { get =>Config.SendTopic; set { Config.SendTopic = value;  NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get =>Config.SubscribeTopic ; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive;set { Config.IsAlive = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }

    }

    public enum  MQTTDeviceType
    {
        Camera = 1,
        PG =2,
        Spectum =3,
        SMU =4,
    }

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
            ServiceConfig.SubscribeTopic = SysResourceModel.Pcode + "/STATUS/" + SysResourceModel.Code;
            ServiceConfig.SendTopic = SysResourceModel.Pcode + "/CMD/" + SysResourceModel.Code;
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除服务" };
            menuItem.Click += (s, e) =>
            {
                this.Parent.RemoveChild(this);
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

        public MQTTDeviceType Type { get => (MQTTDeviceType)SysResourceModel.Type; }


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
