#pragma warning disable CS8604,CS8631
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Cache;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WindowsFormsTest;

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

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; OnPropertyChanged(); } }
        private bool _IsSelected;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;

        public ContextMenu ContextMenu { get; set; }
        [CommandDisplayAttribute("Property", Order = -6), BrowsableAttribute(false)]
        public RelayCommand PropertyCommand { get; set; }
        [CommandDisplayAttribute("Export", Order = -7), BrowsableAttribute(false)]
        public RelayCommand ExportCommand { get; set; }
        [CommandDisplayAttribute("Import", Order = -8), BrowsableAttribute(false)]
        public RelayCommand ImportCommand { get; set; }
        [CommandDisplayAttribute("Copy", Order = -10), BrowsableAttribute(false)]
        public RelayCommand CopyCommand { get; set; }

        [CommandDisplayAttribute("Reset", CommandType = CommandType.Highlighted, Order = 9999)]
        public RelayCommand ResetCommand { get; set; }
        [CommandDisplayAttribute("RestartService",Order =-2)]
        public RelayCommand RefreshCommand { get; set; }

        [CommandDisplayAttribute("ModifyConfiguration",Order =-3)]
        public RelayCommand EditCommand { get; set; }

        [CommandDisplay("FileSavePath",Order =-1)]
        public RelayCommand UpdateFilecfgCommand { get; set; }

        public virtual ImageSource Icon { get; set; }
        public SysResourceModel SysResourceModel { get; set; }

        public virtual UserControl GetDeviceInfo()
        {
            throw new NotImplementedException();
        }

        public virtual UserControl GetDisplayControl()
        {
            return new UserControl();
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

        public SqlSugarClient Db => MySqlControl.GetInstance().DB;
        public T Config { get; set; }

        public override ImageSource Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(); } }
        private ImageSource _Icon;

        public override object GetConfig() => Config;

        public override string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; OnPropertyChanged(); } }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; OnPropertyChanged(); } }


        public DeviceService(SysResourceModel sysResourceModel) : base()
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
                MessageBoxResult result = MessageBox1.Show(WindowHelpers.GetActiveWindow(), $"确定要重置{Name}吗？", "ColorVision", MessageBoxButton.OKCancel);
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
                Window window = new() { Width = 700, Height = 500, Icon = Icon, Title = Properties.Resources.Property };
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

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; OnPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; OnPropertyChanged(); } }
        public override int HeartbeatTime { get => Config.HeartbeatTime; set { Config.HeartbeatTime = value; OnPropertyChanged(); } }

        public event EventHandler ConfigChanged;

        public void SaveConfig()
        {
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            MySqlControl.GetInstance().DB.Updateable(SysResourceModel).ExecuteCommand();
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

        }

        public void RestartRCService()
        {
            string TypeCode =MySqlControl.GetInstance().DB.Queryable<SysDictionaryModel>().Where(x=>x.Pid ==1 && x.Value ==SysResourceModel.Type).First().Key;
            string PCode = MySqlControl.GetInstance().DB.Queryable<SysResourceModel>().InSingle(SysResourceModel.Pid).Code;

            MqttRCService.GetInstance().RestartServices(TypeCode, PCode, Config.Code);
        }


        public override void Delete()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), "非必要情况下请勿删除,是否删除", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            base.Delete();

            Parent.RemoveChild(this);

            //删除数据库
            if (SysResourceModel != null)
                 Db.Deleteable<SysResourceModel>().Where(it => it.Id == SysResourceModel.Id).ExecuteCommand();

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
