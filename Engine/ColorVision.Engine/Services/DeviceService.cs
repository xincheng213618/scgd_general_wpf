#pragma warning disable CS8604,CS8631
using ColorVision.Common.MVVM;
using ColorVision.Engine.Services.Configs;
using ColorVision.Engine.Services.Core;
using ColorVision.Engine.Services.Dao;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using ColorVision.UI.PropertyEditor;
using ColorVision.UI.Views;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services
{
    public abstract class DeviceService : ServiceObjectBase, IDisposable, ITreeViewItem, IIcon
    {
        public virtual string Code { get; set; }
        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        public virtual bool IsAlive { get; set; }
        public virtual DateTime LastAliveTime { get; set; }
        public ServiceTypes ServiceTypes => (ServiceTypes)SysResourceModel.Type;
        public virtual int HeartbeatTime { get; set; }

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; NotifyPropertyChanged(); } }
        private bool _IsSelected;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; NotifyPropertyChanged(); } }
        private bool _IsExpanded = true;

        public ContextMenu ContextMenu { get; set; }

        public RelayCommand PropertyCommand { get; set; }
        public RelayCommand ExportCommand { get; set; }
        public RelayCommand ImportCommand { get; set; }
        public RelayCommand CopyCommand { get; set; }
        public RelayCommand ResetCommand { get; set; }
        public RelayCommand RefreshCommand { get; set; }

        [CommandDisplayAttribute("ModifyConfiguration")]
        public RelayCommand EditCommand { get; set; }
        public RelayCommand UpdateFilecfgCommand { get; set; }

        public virtual ImageSource Icon { get; set; }
        public SysDeviceModel SysResourceModel { get; set; }

        public virtual UserControl GetDeviceInfo()
        {
            throw new NotImplementedException();
        }

        public bool IsDisplayOpen { get => _IsDisplayOpen; set { _IsDisplayOpen = value; NotifyPropertyChanged(); } }
        private bool _IsDisplayOpen = true;

        public virtual UserControl GetDisplayControl()
        {
            return new UserControl();
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

        public virtual MQTTServiceBase? GetMQTTService()
        {
            return null;
        }
        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class DeviceService<T> : DeviceService where T : DeviceServiceConfig, new()
    {
        public T Config { get; set; }

        public override ImageSource Icon { get => _Icon; set { _Icon = value; NotifyPropertyChanged(); } }
        private ImageSource _Icon;

        public override object GetConfig() => Config;

        public override string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; NotifyPropertyChanged(); } }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; NotifyPropertyChanged(); } }


        public DeviceService(SysDeviceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            ContextMenu = new ContextMenu();

            ExportCommand = new RelayCommand(a =>
            {
                System.Windows.Forms.SaveFileDialog ofd = new();
                ofd.Filter = "*.config|*.config";
                ofd.FileName = Config?.Name;
                ofd.RestoreDirectory = true;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
                Config.ToJsonNFile(ofd.FileName);
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), "导出成功", "ColorVision");
            });

            CopyCommand = new RelayCommand(a =>
            {
                if (Config != null)
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), Config.ToJsonN(), "ColorVision");
                }
            });
            ResetCommand = new RelayCommand(a =>
            {
                MessageBoxResult result = MessageBox1.Show(WindowHelpers.GetActiveWindow(), "确定要重置吗？", "ColorVision", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.OK)
                    Config = new T();
            }, a => AccessControl.Check(PermissionMode.Administrator));
            DeleteCommand = new RelayCommand(a => Delete(), a => AccessControl.Check(PermissionMode.Administrator));
            EditCommand = new RelayCommand(a => { });

            ImportCommand = new RelayCommand(a =>
            {
                System.Windows.Forms.SaveFileDialog ofd = new();
                ofd.Filter = "*.config|*.config";
                ofd.RestoreDirectory = true;
                ofd.SupportMultiDottedExtensions = false;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string data = File.ReadAllText(ofd.FileName);
                var config = JsonConvert.DeserializeObject<T>(data);
                if (config != null)
                {
                    config.CopyTo(Config);
                    Save();
                }
                else
                    MessageBox.Show("导入异常", "ColorVision");
            });

            PropertyCommand = new RelayCommand((e) =>
            {
                Window window = new() { Width = 700, Height = 400, Icon = Icon, Title = Properties.Resources.Property };
                window.Content = GetDeviceInfo();
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.ApplyCaption();
                window.ShowDialog();
            });
            RefreshCommand = new RelayCommand(a => RestartRCService());

            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Export, Command = ExportCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Import, Command = ImportCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = PropertyCommand });


            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<T>(SysResourceModel.Value);

            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = SysResourceModel.Name ?? string.Empty;
            UpdateFilecfgCommand = new RelayCommand(a => UpdateFilecfg(), a=> Config is IFileServerCfg);
        }

        public void UpdateFilecfg()
        {
            if (Config is IFileServerCfg fileServerCfg)
            {
                var oldvalue = fileServerCfg.FileServerCfg.Clone();

                var window = new PropertyEditorWindow(fileServerCfg.FileServerCfg, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
                window.Closed += (s, e) =>
                {
                    if (!fileServerCfg.FileServerCfg.EqualMax(oldvalue))
                    {
                        Save();
                    }
                };
                window.ShowDialog();
            }
        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; NotifyPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; NotifyPropertyChanged(); } }
        public override bool IsAlive { get => Config.IsAlive; set { Config.IsAlive = value; NotifyPropertyChanged(); } }
        public override DateTime LastAliveTime { get => Config.LastAliveTime; set { Config.LastAliveTime = value; NotifyPropertyChanged(); } }
        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; NotifyPropertyChanged(); } }

        public event EventHandler ConfigChanged;

        public void SaveConfig()
        {
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            VSysResourceDao.Instance.Save(new SysResourceModel(SysResourceModel));
        }

        public override void Save()
        {
            base.Save();
            SaveConfig();

            RestartRCService();

            OnConfigChanged();
            ConfigChanged?.Invoke(this, new EventArgs());
        }

        protected virtual void OnConfigChanged()
        {
            // 这里可以放置你希望在配置变更时执行的逻辑
            // 比如记录日志，刷新缓存等
            Console.WriteLine("Configuration has changed.");
        }

        public void RestartRCService()
        {
            MqttRCService.GetInstance().RestartServices(SysResourceModel.TypeCode, SysResourceModel.PCode, Config.Code);
        }


        public override void Delete()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), "是否删除", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            base.Delete();
            Parent.RemoveChild(this);

            //删除数据库
            if (SysResourceModel != null)
                VSysResourceDao.Instance.DeleteById(SysResourceModel.Id);
            //删除设备服务
            ServiceManager.GetInstance().DeviceServices.Remove(this);
            //删除前台显示
            if (GetDisplayControl() is IDisPlayControl disPlayControl)
                DisPlayManager.GetInstance().IDisPlayControls.Remove(disPlayControl);
            //删除资源

            Dispose();
        }
    }
}
