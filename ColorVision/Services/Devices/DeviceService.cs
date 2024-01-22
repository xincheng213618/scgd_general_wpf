#pragma warning disable  CS8604,CS8631
using ColorVision.Extension;
using ColorVision.Handler;
using ColorVision.MVVM;
using ColorVision.RC;
using ColorVision.Services.Dao;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Services.Devices
{
    public class DeviceService : BaseObject, IDisposable
    {
        public virtual string Code { get; set; }

        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }
        public virtual DateTime LastAliveTime { get; set; }

        public virtual int HeartbeatTime { get; set; }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public RelayCommand PropertyCommand { get; set; }
        public RelayCommand ExportCommand { get; set; }
        public RelayCommand ImportCommand { get; set; }
        public RelayCommand CopyCommand { get; set; }
        public RelayCommand ResetCommand { get; set; }

        public RelayCommand EditCommand { get; set; }
        public bool IsEditMode { get => _IsEditMode; set { _IsEditMode = value; NotifyPropertyChanged(); } }
        private bool _IsEditMode;



        public virtual ImageSource Icon { get; set; }


        public SysResourceModel SysResourceModel { get; set; }

        public virtual UserControl GetDeviceControl()
        {
            throw new NotImplementedException();
        }

        public virtual UserControl GetDeviceInfo()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayOpen { get => _IsDisplayOpen; set { _IsDisplayOpen = value; NotifyPropertyChanged(); } }
        private bool _IsDisplayOpen = true;

        public virtual UserControl GetDisplayControl()
        {
            throw new NotImplementedException();
        }

        public virtual UserControl GetEditControl()
        {
            throw new NotImplementedException();
        }

        public virtual View GetView()
        {
            throw new NotImplementedException();
        }

        //继承Config
        public virtual object GetConfig()
        {
            throw new NotImplementedException();
        }


        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class DeviceService<T> : DeviceService where T :DeviceServiceConfig,new()
    {
        public T Config { get; set; }

        public override ImageSource Icon { get => _Icon; set { _Icon = value; NotifyPropertyChanged(); } }
        private ImageSource _Icon;
        public  ImageSource? QRIcon { get => _QRIcon; set { _QRIcon = value; NotifyPropertyChanged(); } }
        private ImageSource? _QRIcon;

        public override object GetConfig() => Config;

        public override string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set{ SysResourceModel.Name = value; NotifyPropertyChanged(); } }

        public int MySqlId { get => SysResourceModel.Id; }

        public DeviceService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            ContextMenu = new ContextMenu();
            MenuItem menuItem = new MenuItem() { Header = "删除资源" };
            menuItem.Click += (s, e) =>
            {
                Delete();
            };
            ContextMenu.Items.Add(menuItem);
            ExportCommand = new RelayCommand(a => {
                System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                ofd.Filter = "*.config|*.config";
                ofd.FileName = Config?.Name;
                ofd.RestoreDirectory = true;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                Config.ToJsonNFile(ofd.FileName);
                MessageBox.Show("导出成功", "ColorVision"); 
            });

            CopyCommand = new RelayCommand(a => {
                if (Config!=null)
                {
                    NativeMethods.Clipboard.SetText(Config.ToJsonN());
                    MessageBox.Show("复制成功", "ColorVision");
                }
            });
            ResetCommand = new RelayCommand(a => {
                MessageBoxResult result = MessageBox.Show("确定要重置吗？", "ColorVision", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                    Config = new T();
            });

            EditCommand = new RelayCommand(a =>
            {
                IsEditMode = true;
            });

            ImportCommand = new RelayCommand(a => {
                System.Windows.Forms.SaveFileDialog ofd = new System.Windows.Forms.SaveFileDialog();
                ofd.Filter = "*.config|*.config";
                ofd.RestoreDirectory = true;
                ofd.SupportMultiDottedExtensions = false;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string data = File.ReadAllText(ofd.FileName);
                var config = JsonConvert.DeserializeObject<T>(data);
                if (config != null)
                {
                    config.CopyTo(this.Config);
                    Save();
                }
                else
                    MessageBox.Show("导入异常","ColorVision");
            });

            MenuItem menuItemExport = new MenuItem() { Header = "导出配置", Command = ExportCommand };
            ContextMenu.Items.Add(menuItemExport);

            MenuItem menuItemImport = new MenuItem() { Header = "导入配置", Command = ImportCommand };
            ContextMenu.Items.Add(menuItemImport);

            PropertyCommand = new RelayCommand((e) =>
            {
                Window window = new Window() { Width = 400, Height=400 , Title = Properties.Resource.Property};
                window.Content = GetDeviceInfo();
                window.Owner = Application.Current.MainWindow;
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ShowDialog();
            });

            MenuItem menuItemProperty = new MenuItem() { Header = Properties.Resource.Property, Command = PropertyCommand };
            ContextMenu.Items.Add(menuItemProperty);

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
            Config.Name = SysResourceModel.Name ?? string.Empty;
            QRIcon = QRCodeHelper.GetQRCode("http://m.color-vision.com/sys-pd/1.html");

        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }
        public override DateTime LastAliveTime { get => Config.LastAliveTime; set { Config.LastAliveTime = value; NotifyPropertyChanged(); } }
        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public override void Save()
        {
            base.Save();
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            ServiceManager.GetInstance().ResourceService.Save(SysResourceModel);
            IsEditMode = false;

            ///每次提交之后重启服务
            MQTTRCService.GetInstance().RestartServices();
            QRIcon = QRCodeHelper.GetQRCode("http://m.color-vision.com/sys-pd/1.html");
        }



        public override void Delete()
        {
            base.Delete();
            if (SysResourceModel != null)
                ServiceManager.GetInstance().ResourceService.DeleteById(SysResourceModel.Id);
            Parent.RemoveChild(this);
            this.Dispose();
        }
    }
}
