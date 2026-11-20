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
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services
{
    internal sealed class DeviceConfigurationTarget
    {
        private readonly DeviceService device;
        private readonly SysResourceModel resource;
        private readonly MySqlConnectedEndpoint? endpoint;
        private readonly SysResourceDao? localResources;
        private readonly int id;
        private string? code;
        private readonly int type;
        private readonly int tenant;
        private readonly int? parentId;
        private readonly object? configuration;

        internal DeviceConfigurationTarget(DeviceService device, SysResourceDao? localResources)
        {
            this.device = device;
            resource = device.SysResourceModel;
            id = resource.Id;
            code = resource.Code;
            type = resource.Type;
            tenant = resource.TenantId;
            parentId = resource.Pid;
            endpoint = id > 0 ? device.ResultDatabaseEndpoint : null;
            this.localResources = localResources;
            configuration = device.ConfigurationIdentity;
        }

        internal bool IsCurrentResource(DeviceService current) => ReferenceEquals(current, device)
            && !current.IsConfigurationRetired && ReferenceEquals(current.SysResourceModel, resource)
            && ReferenceEquals(current.ConfigurationIdentity, configuration) && Matches(resource);

        internal bool IsCurrent(DeviceService current) => IsCurrentResource(current)
            && (id > 0 ? endpoint?.IsCurrent == true && ReferenceEquals(current.ResultDatabaseEndpoint, endpoint)
                : !SysResourceDao.IsLocalId(id) || ReferenceEquals(current.LocalConfigurationResourceDao ?? SysResourceDao.Instance, localResources));

        internal MySqlConnectedEndpoint? Endpoint => endpoint;
        internal SysResourceDao? LocalResources => localResources;

        private bool Matches(SysResourceModel row) => row.Id == id && row.Type == type && row.TenantId == tenant
            && row.Pid == parentId && string.Equals(row.Code, code, StringComparison.Ordinal) && !row.IsDelete;

        internal bool TryResolveRestart(DeviceService current, out (string TypeCode, string ParentCode, string DeviceCode) route)
        {
            route = default;
            if (endpoint == null || parentId == null || string.IsNullOrWhiteSpace(code) || !IsCurrent(current)) return false;

            using var db = endpoint.CreateDbClient();
            if (!IsCurrent(current)) return false;
            var row = db.Queryable<SysResourceModel>().First(item => item.Id == id);
            if (row == null || !Matches(row)) return false;

            var typeCode = db.Queryable<SysDictionaryModel>().Where(item => item.Pid == 1 && item.Value == type).Select(item => item.Key).First();
            var parentCode = db.Queryable<SysResourceModel>().Where(item => item.Id == parentId && !item.IsDelete).Select(item => item.Code).First();
            if (string.IsNullOrWhiteSpace(typeCode) || string.IsNullOrWhiteSpace(parentCode) || !IsCurrent(current)) return false;

            route = (typeCode, parentCode, code);
            return true;
        }

        internal bool TrySave(DeviceService current, Func<string?, string> updateValue)
        {
            if (!IsCurrent(current)) return false;
            if (id is 0 or -1) return true;

            string? value = null;
            bool saved;
            if (localResources != null)
            {
                saved = localResources.TryUpdateLocal(id, row =>
                {
                    if (!Matches(row) || !IsCurrent(current)) return false;
                    value = row.Value = updateValue(row.Value);
                    return true;
                });
            }
            else if (endpoint != null)
            {
                using var db = endpoint.CreateDbClient();
                if (!IsCurrent(current)) return false;
                var row = db.Queryable<SysResourceModel>().First(item => item.Id == id);
                if (row == null || !Matches(row)) return false;

                string? previous = row.Value;
                value = updateValue(previous);
                if (!IsCurrent(current)) return false;
                saved = string.Equals(previous, value, StringComparison.Ordinal) || db.Updateable<SysResourceModel>()
                    .SetColumns(item => item.Value == value)
                    .Where(item => item.Id == id && item.Code == code && item.Type == type && item.TenantId == tenant
                        && item.Pid == parentId && !item.IsDelete && item.Value == previous).ExecuteCommand() == 1;
            }
            else return false;

            if (!saved || !IsCurrent(current)) return false;
            resource.Value = value;
            return true;
        }

        internal bool TrySaveConfiguration(DeviceService current, string nextCode, string name, string value)
        {
            if (!IsCurrent(current)) return false;

            bool saved;
            if (localResources != null)
            {
                saved = localResources.TryUpdateLocal(id, row =>
                {
                    if (!Matches(row) || !IsCurrent(current)) return false;
                    row.Code = nextCode;
                    row.Name = name;
                    row.Value = value;
                    return true;
                });
            }
            else if (endpoint != null)
            {
                using var db = endpoint.CreateDbClient();
                if (!IsCurrent(current)) return false;
                var row = db.Queryable<SysResourceModel>().First(item => item.Id == id);
                if (row == null || !Matches(row) || !IsCurrent(current)) return false;

                saved = (string.Equals(row.Code, nextCode, StringComparison.Ordinal)
                    && string.Equals(row.Name, name, StringComparison.Ordinal)
                    && string.Equals(row.Value, value, StringComparison.Ordinal))
                    || db.Updateable<SysResourceModel>()
                        .SetColumns(item => new SysResourceModel { Code = nextCode, Name = name, Value = value })
                        .Where(item => item.Id == id && item.Code == code && item.Type == type && item.TenantId == tenant
                            && item.Pid == parentId && !item.IsDelete && item.Name == row.Name && item.Value == row.Value)
                        .ExecuteCommand() == 1;
            }
            else return false;

            if (!saved) return false;
            if (IsCurrent(current))
            {
                resource.Code = code = nextCode;
                resource.Name = name;
                resource.Value = value;
            }
            return true;
        }
    }

    public abstract class DeviceService : ServiceObjectBase, IDisposable, ITreeViewItem, ICopilotBusinessContextSource
    {
        private static readonly ILog ConfigurationLog = LogManager.GetLogger(typeof(DeviceService));
        private static string RetiredConfigurationMessage => EngineLocalization.Get("原设备或配置库已失效，请关闭窗口后重新选择设备。");
        private int _configurationRetired;

        internal bool IsConfigurationRetired => Volatile.Read(ref _configurationRetired) != 0;
        internal void RetireConfiguration() => Interlocked.Exchange(ref _configurationRetired, 1);
        internal virtual object? ConfigurationIdentity => null;

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

        public SysResourceModel SysResourceModel { get; set; }
        internal MySqlConnectedEndpoint? ResultDatabaseEndpoint { get; set; }
        internal SysResourceDao? LocalConfigurationResourceDao { get; set; }
        internal DeviceConfigurationTarget CaptureConfigurationTarget() => new(this,
            SysResourceDao.IsLocalId(SysResourceModel.Id) ? LocalConfigurationResourceDao ?? SysResourceDao.Instance : null);
        protected override string? AuthorizationResourceId => ServiceAuthorization.ResourceId(SysResourceModel);

        internal bool CanConfigure => ServiceAuthorization.Can("device.configure", SysResourceModel);

        internal void DemandConfigurationAccess() => ServiceAuthorization.Demand("device.configure", SysResourceModel);

        internal DeviceConfigurationTarget ProtectConfigurationEditor(PropertyEditorWindow window)
        {
            var target = CaptureConfigurationTarget();
            window.Submitting += (_, e) =>
            {
                if (!CanConfigure)
                {
                    e.Cancel = true;
                    e.ErrorMessage = EngineLocalization.Get("当前权限不足，无法保存设备配置。");
                }
                else if (!target.IsCurrent(this))
                {
                    e.Cancel = true;
                    e.ErrorMessage = RetiredConfigurationMessage;
                }
            };
            return target;
        }

        internal bool TryAdmitConfigurationEdit(DeviceConfigurationTarget target, Window? owner)
        {
            if (target.IsCurrent(this)) return true;
            if (owner != null) MessageBox.Show(owner, RetiredConfigurationMessage, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        private protected void RequireConfigurationTarget(DeviceConfigurationTarget target)
        {
            if (!target.IsCurrent(this)) throw new DeviceConfigurationSaveException(RetiredConfigurationMessage);
        }

        internal void CommitConfiguration(Action? prepare = null, DeviceConfigurationTarget? expectedTarget = null)
        {
            DemandConfigurationAccess();
            SaveEditedConfiguration(expectedTarget ?? CaptureConfigurationTarget(), prepare);
        }

        private protected void SaveEditedConfiguration(DeviceConfigurationTarget target, Action? prepare)
        {
            RequireConfigurationTarget(target);
            prepare?.Invoke();
            RequireConfigurationTarget(target);
            Save();
        }

        internal bool TryCommitConfiguration(Window? owner, Action? prepare = null, DeviceConfigurationTarget? expectedTarget = null)
            => TryConfigurationAction(owner, () => CommitConfiguration(prepare, expectedTarget));

        private protected static bool TryConfigurationAction(Window? owner, Action action)
        {
            try { action(); return true; }
            catch (AccessDeniedException)
            {
                ShowConfigurationWarning(owner, EngineLocalization.Get("当前权限不足，无法保存设备配置。"));
                return false;
            }
            catch (DeviceConfigurationSaveException error)
            {
                ConfigurationLog.Warn("Device configuration could not be saved.", error);
                ShowConfigurationWarning(owner, error.Message);
                return false;
            }
        }

        private static void ShowConfigurationWarning(Window? owner, string message)
        {
            if (owner == null) MessageBox.Show(message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            else MessageBox.Show(owner, message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private protected sealed class DeviceConfigurationSaveException(string message, Exception? inner = null) : InvalidOperationException(message, inner);

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

        public override void Delete() => DeleteOwnedResource(DeleteWithConfirmation);

        internal bool DeleteWithConfirmation(Func<bool> confirm)
        {
            DemandDeleteAccess();
            var configuration = CaptureConfigurationTarget();
            var target = new ServiceResourceTarget(SysResourceModel, configuration.Endpoint, configuration.LocalResources);
            var parent = Parent;
            bool IsCurrent() => ReferenceEquals(parent, Parent) && configuration.IsCurrent(this)
                && target.IsCurrent(SysResourceModel, configuration.Endpoint, configuration.LocalResources);
            if (!IsCurrent() || !confirm()) return false;
            DemandDeleteAccess();
            if (!target.TryDelete(includeDirectChildren: false, IsCurrent)) return false;

            CompleteResourceDeletion(configuration);
            return true;
        }

        internal void CompleteResourceDeletion(DeviceConfigurationTarget? configuration = null)
        {
            if (IsConfigurationRetired) return;
            if (configuration?.IsCurrent(this) == true)
            {
                try { OnResourceDeleted(configuration); }
                catch (Exception error) { ConfigurationLog.Warn("Device resource was deleted, but an associated cleanup failed.", error); }
            }

            RetireConfiguration();
            try
            {
                Parent?.RemoveChild(this);
                ServiceManager.Current?.ForgetDeletedDevice(this);
            }
            catch (Exception error) { ConfigurationLog.Warn("Device resource was deleted, but its display could not be removed.", error); }
            try { Dispose(); }
            catch (Exception error) { ConfigurationLog.Warn("Device resource was deleted, but the device could not be disposed.", error); }
        }

        private protected virtual void OnResourceDeleted(DeviceConfigurationTarget configuration) { }

        public virtual void Dispose()
        {
            RetireConfiguration();
            GC.SuppressFinalize(this);
        }
    }


    public class DeviceService<T> : DeviceService where T : DeviceServiceConfig, new()
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(DeviceService<T>));
        public T Config { get; set; }
        internal override object? ConfigurationIdentity => Config;
        internal Action<string, string, string>? RestartRequest { get; set; }


        public override object GetConfig() => Config;

        public override string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; OnPropertyChanged(); } }
        public override string Name { get => SysResourceModel.Name ?? string.Empty; set { SysResourceModel.Name = value; OnPropertyChanged(); } }




        public DeviceService(SysResourceModel sysResourceModel) : base()
        {
            SysResourceModel = sysResourceModel;
            if (sysResourceModel.Id > 0)
                ResultDatabaseEndpoint = MySqlControl.GetInstance().ConnectedEndpoint;
            else if (SysResourceDao.IsLocalId(sysResourceModel.Id))
                LocalConfigurationResourceDao = SysResourceDao.Instance;
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
                var target = CaptureConfigurationTarget();
                System.Windows.Forms.SaveFileDialog ofd = new();
                ofd.Filter = "*.config|*.config";
                ofd.RestoreDirectory = true;
                ofd.SupportMultiDottedExtensions = false;
                if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string data = File.ReadAllText(ofd.FileName);
                var config = JsonConvert.DeserializeObject<T>(data);
                if (config != null)
                {
                    var owner = Application.Current.GetActiveWindow();
                    TryConfigurationAction(owner, () => SaveEditedConfiguration(target, () => config.CopyTo(Config)));
                }
                else
                    MessageBox.Show(Properties.Resources.ImportException, "ColorVision");
            });

            PropertyCommand = new RelayCommand((e) =>
            {
                new DevicePropertyWindow(Name, Code, GetDeviceInfo())
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                }.ShowDialog();
            });
            AskCopilotCommand = new RelayCommand(_ => AskCopilotAboutDevice());
            RefreshCommand = new RelayCommand(_ => TryConfigurationAction(Application.Current.GetActiveWindow(), Save));
            SaveCommand = RefreshCommand;

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

        public void SaveConfig() => SaveConfiguration(CaptureConfigurationTarget());

        internal bool TrySaveConfig(Window? owner, DeviceConfigurationTarget target, Action? prepare = null)
            => TryConfigurationAction(owner, () =>
            {
                RequireConfigurationTarget(target);
                prepare?.Invoke();
                SaveConfiguration(target);
            });

        private void SaveConfiguration(DeviceConfigurationTarget target)
        {
            RequireConfigurationTarget(target);
            bool saved;
            try
            {
                saved = target.TrySaveConfiguration(this, Config.Code, Config.Name, JsonConvert.SerializeObject(Config));
            }
            catch (Exception error)
            {
                throw new DeviceConfigurationSaveException(EngineLocalization.Get("设备配置写入失败，详情请查看日志。"), error);
            }
            if (!saved) throw new DeviceConfigurationSaveException(EngineLocalization.Get("设备配置已失效或记录已被修改，请重新选择设备后重试。"));
        }

        public override void Save() => SaveWithConfigurationTarget();

        private protected void SaveWithConfigurationTarget(Action<DeviceConfigurationTarget>? prepare = null, Action<DeviceConfigurationTarget>? apply = null,
            bool applyAfterNotification = false)
        {
            var target = CaptureConfigurationTarget();
            RequireConfigurationTarget(target);
            prepare?.Invoke(target);
            base.Save();
            SaveConfiguration(target);
            if (!target.IsCurrent(this)) return;
            if (!applyAfterNotification) apply?.Invoke(target);
            if (!target.IsCurrent(this)) return;
            RestartRCService();
            if (!target.IsCurrent(this)) return;
            NotifyConfigChanged();
            if (applyAfterNotification && target.IsCurrent(this)) apply?.Invoke(target);
        }

        protected void NotifyConfigChanged()
        {
            OnConfigChanged();
            ConfigChanged?.Invoke(this, new EventArgs());
        }

        protected virtual void OnConfigChanged()
        {

        }

        public virtual void RestartRCService()
        {
            if (SysResourceModel.Id <= 0) return;
            var target = CaptureConfigurationTarget();
            try
            {
                if (!target.TryResolveRestart(this, out var route)) return;
                if (RestartRequest is { } request) request(route.TypeCode, route.ParentCode, route.DeviceCode);
                else _ = MqttRCService.GetInstance().TryRestartServices(route.TypeCode, route.ParentCode, route.DeviceCode,
                    () => target.IsCurrent(this));
            }
            catch (Exception error)
            {
                log.Warn("Could not request restart for the owning device configuration.", error);
            }
        }

    }
}
