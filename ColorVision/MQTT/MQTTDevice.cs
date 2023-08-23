using ColorVision.MQTT.Service;
using ColorVision.MQTT.SMU;
using ColorVision.MySql.DAO;
using Newtonsoft.Json;
using System;
using System.Data;
using System.Windows.Controls;

namespace ColorVision.MQTT
{
    public class MQTTDevice : BaseObject, IDisposable
    {
        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }
        public virtual DateTime LastAliveTime { get; set; }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class MQTTDevice<T> : MQTTDevice where T :BaseDeviceConfig,new()
    {
        public T Config { get; set; }
        public SysResourceModel SysResourceModel { get; set; }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public MQTTDevice(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除资源" };
            menuItem.Click += (s, e) =>
            {
                Parent.RemoveChild(this);
                if (SysResourceModel != null)
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);

            };
            ContextMenu.Items.Add(menuItem);


            if (string.IsNullOrEmpty(SysResourceModel.Value))
            {
                Config = new T();
            }
            else
            {
                try
                {
                    Config = JsonConvert.DeserializeObject<T>(SysResourceModel.Value) ?? new T();
                }
                catch
                {
                    Config = new T();
                }
            }
            Config.Code = SysResourceModel.Code ?? string.Empty;
        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }
        public override DateTime LastAliveTime { get => Config.LastAliveTime; set { Config.LastAliveTime = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceControl.GetInstance().ResourceService.Save(SysResourceModel);
        }
    }
}
