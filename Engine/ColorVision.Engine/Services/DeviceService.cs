#pragma warning disable CA1859,CS8604,CS8631
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Engine.Services
{
    public abstract class DeviceService : ServiceObjectBase, IDisposable, ITreeViewItem, IIcon, ICopilotBusinessContextSource
    {
        public virtual string Code { get; set; }
        public virtual string SendTopic { get; set; }
        public virtual string SubscribeTopic { get; set; }
        private int _observedAliveState;
        public virtual bool IsAlive
        {
            get => Volatile.Read(ref _observedAliveState) == 2;
            set => Volatile.Write(ref _observedAliveState, value ? 2 : 1);
        }
        internal bool? ObservedIsAlive
        {
            get
            {
                int state = Volatile.Read(ref _observedAliveState);
                return state == 0 ? null : state == 2;
            }
        }
        public virtual DateTime LastAliveTime { get; set; }
        public ServiceTypes ServiceTypes => (ServiceTypes)SysResourceModel.Type;

        public bool IsSelected { get => _IsSelected; set { _IsSelected = value; OnPropertyChanged(); } }
        private bool _IsSelected;

        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;

        public ContextMenu ContextMenu { get; set; }
        [CommandDisplayAttribute("Property", Order = -6), BrowsableAttribute(false)]
        public RelayCommand PropertyCommand { get; set; }
        [CommandDisplayAttribute("Ask Copilot", Order = -5), BrowsableAttribute(false)]
        public RelayCommand AskCopilotCommand { get; set; }
        [CommandDisplayAttribute("Export", Order = -7), BrowsableAttribute(false)]
        public RelayCommand ExportCommand { get; set; }
        [CommandDisplayAttribute("Import", Order = -8), BrowsableAttribute(false)]
        public RelayCommand ImportCommand { get; set; }
        [CommandDisplayAttribute("Copy", Order = -10), BrowsableAttribute(false)]
        public RelayCommand CopyCommand { get; set; }

        [CommandDisplayAttribute("RestartService",Order =-2, CategoryOrder = 4), Browsable(false)]
        [Category("MaintenanceDiagnostics")]
        [Description("CommandRestartHint")]
        public RelayCommand RefreshCommand { get; set; }

        [CommandDisplayAttribute("ModifyConfiguration",Order =-3, CategoryOrder = 0)]
        [Category("DeviceConnection")]
        [Description("CommandEditConfigHint")]
        public RelayCommand EditCommand { get; set; }

        public virtual ImageSource Icon { get; set; }
        public SysResourceModel SysResourceModel { get; set; }
        protected override string? AuthorizationResourceId => ServiceAuthorization.ResourceId(SysResourceModel);

        internal bool CanConfigure => ServiceAuthorization.Can("device.configure", SysResourceModel);

        internal void DemandConfigurationAccess() => ServiceAuthorization.Demand("device.configure", SysResourceModel);

        internal void ProtectConfigurationEditor(PropertyEditorWindow window)
        {
            window.Submitting += (_, e) =>
            {
                if (CanConfigure) return;
                e.Cancel = true;
                e.ErrorMessage = EngineLocalization.Get("当前权限不足，无法保存设备配置。");
            };
        }

        internal void CommitConfiguration(Action? prepare = null)
        {
            DemandConfigurationAccess();
            prepare?.Invoke();
            Save();
        }

        internal bool TryCommitConfiguration(Window owner, Action? prepare = null)
        {
            try { CommitConfiguration(prepare); return true; }
            catch (AccessDeniedException)
            {
                MessageBox.Show(owner, EngineLocalization.Get("当前权限不足，无法保存设备配置。"), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

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

        public abstract CopilotBusinessContextBundle CaptureCopilotContext();


        public event EventHandler<MsgRecord> MsgRecordChanged;

        public virtual void SetMsgRecordChanged(MsgRecord msgRecord)
        {
            MsgRecordChanged?.Invoke(this, msgRecord);
        }

        public virtual void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }


    public class DeviceService<T> : DeviceService where T : DeviceServiceConfig, new()
    {
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
                MessageBox1.Show(WindowHelpers.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ExportSucceeded, "ColorVision");
            });

            CopyCommand = new RelayCommand(a =>
            {
                if (Config != null)
                {
                    MessageBox1.Show(WindowHelpers.GetActiveWindow(), Config.ToJsonN(), "ColorVision");
                }
            });
            DeleteCommand = new RelayCommand(a => DeleteFromCommand(), a => CanDelete);
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
                    MessageBox.Show(Properties.Resources.ImportException, "ColorVision");
            });

            PropertyCommand = new RelayCommand((e) =>
            {
                new DevicePropertyWindow(Icon, Name, Code, GetDeviceInfo())
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.ShowDialog();
            });
            AskCopilotCommand = new RelayCommand(_ => AskCopilotAboutDevice());
            RefreshCommand = new RelayCommand(a => Save());

            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.RestartService, Command = RefreshCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = PropertyCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Copilot_Ask, Command = AskCopilotCommand });


            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<T>(SysResourceModel.Value);

            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = SysResourceModel.Name ?? string.Empty;
        }

        public override CopilotBusinessContextBundle CaptureCopilotContext()
        {
            return CopilotDeviceContextFactory.Capture(this);
        }

        private void AskCopilotAboutDevice()
        {
            var bundle = CaptureCopilotContext();
            var snapshot = new CopilotDeviceContextSnapshot
            {
                SourceId = bundle.SourceId,
                Title = bundle.Title,
                ServiceName = Name ?? string.Empty,
                ServiceCode = Code ?? string.Empty,
                ServiceType = ServiceTypes.ToString(),
            };
            var result = CopilotBusinessContextCoordinator.DispatchDiagnosis(bundle, CopilotBusinessContextCoordinator.BuildDeviceDiagnosisPrompt(snapshot));
            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK,
                    result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
        }

        public override string SendTopic { get => Config.SendTopic; set { Config.SendTopic = value; OnPropertyChanged(); } }
        public override string SubscribeTopic { get => Config.SubscribeTopic; set { Config.SubscribeTopic = value; OnPropertyChanged(); } }

        public event EventHandler ConfigChanged;

        public void SaveConfig()
        {
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            SysResourceDao.Instance.Save(SysResourceModel);
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

        public virtual void RestartRCService()
        {
            if (SysResourceDao.IsLocalId(SysResourceModel.Id)) return;
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            string TypeCode =DB.Queryable<SysDictionaryModel>().Where(x=>x.Pid ==1 && x.Value ==SysResourceModel.Type).First().Key;
            string PCode = DB.Queryable<SysResourceModel>().InSingle(SysResourceModel.Pid).Code;

            MqttRCService.GetInstance().RestartServices(TypeCode, PCode, Config.Code);
        }

        public override void Delete()
        {
            DemandDeleteAccess();
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.AvoidDeletionUnlessNecessary, "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            base.Delete();

            Parent.RemoveChild(this);

            //删除数据库
            if (SysResourceModel != null)
            {
                SysResourceDao.Instance.DeleteById(SysResourceModel.Id);

            }

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
