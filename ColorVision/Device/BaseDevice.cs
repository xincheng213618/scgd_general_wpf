using ColorVision.Device;
using ColorVision.MySql.DAO;
using ColorVision.Services;
using Newtonsoft.Json;
using System;
using System.Windows.Controls;

namespace ColorVision.MQTT
{
    public class BaseDevice : BaseObject, IDisposable
    {
        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }
        public virtual DateTime LastAliveTime { get; set; }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public virtual UserControl GetDeviceControl()
        {
            throw new NotImplementedException();
        }

        public virtual UserControl GetDisplayControl()
        {
            throw new NotImplementedException();
        }
        public virtual View GetView()
        {
            throw new NotImplementedException();
        }


        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class BaseDevice<T> : BaseDevice where T :BaseDeviceConfig,new()
    {
        public T Config { get; set; }
        public SysResourceModel SysResourceModel { get; set; }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }
        public string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }


        public BaseDevice(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;

            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除资源" };
            menuItem.Click += (s, e) =>
            {
                if (SysResourceModel != null)
                    ServiceControl.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
                Parent.RemoveChild(this);

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
