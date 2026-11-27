using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;


namespace ColorVision.Engine.Services.Terminal
{
    public class TerminalServiceBase : ServiceObjectBase, ITreeViewItem
    {
        public bool IsExpanded { get => _IsExpanded; set { _IsExpanded = value; OnPropertyChanged(); } }
        private bool _IsExpanded = true;

        public bool IsSelected { get => _IsChecked; set { _IsChecked = value; OnPropertyChanged(); } }
        private bool _IsChecked;
        public ContextMenu ContextMenu { get; set; }


        public virtual UserControl GenDeviceControl()
        {
            throw new System.NotImplementedException();
        }
    }

    public class TerminalService : TerminalServiceBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TerminalService));
        private int retired;
        internal bool IsConfigurationRetired => Volatile.Read(ref retired) != 0;

        public SysResourceModel SysResourceModel { get; set; }
        internal MySqlConnectedEndpoint? ResourceDatabaseEndpoint { get; set; }
        internal SysResourceDao? LocalConfigurationResourceDao { get; set; }
        internal Func<(string TypeCode, string ServiceCode), IReadOnlyList<string>, Func<bool>, bool>? ReloadRequest { get; set; }
        internal TerminalConfigurationTarget CaptureConfigurationTarget() => new(this);
        protected override string? AuthorizationResourceId => ServiceAuthorization.ResourceId(SysResourceModel);
        public TerminalServiceConfig Config { get; set; }

        public MQTTServiceTerminalBase MQTTServiceTerminalBase { get; set; }

        public ServiceTypes ServiceType { get => (ServiceTypes)SysResourceModel.Type; }

        public override string Name { get => SysResourceModel.Name??string.Empty ; set { SysResourceModel.Name = value; OnPropertyChanged(); } }

        public string Code { get => SysResourceModel.Code ?? string.Empty; set { SysResourceModel.Code = value; OnPropertyChanged(); } }

        public RelayCommand RefreshCommand { get; set; }
        public RelayCommand EditCommand { get; set; }
        public RelayCommand OpenCreateWindowCommand { get; set; }
        public TerminalService(SysResourceModel sysResourceModel) : this(sysResourceModel, transport: null) { }

        internal TerminalService(SysResourceModel sysResourceModel, MQTTServiceTerminalBase? transport)
        {
            SysResourceModel = sysResourceModel;
            if (sysResourceModel.Id > 0) ResourceDatabaseEndpoint = WorkstationConfiguration.ConnectedEndpoint;
            else if (SysResourceDao.IsLocalId(sysResourceModel.Id)) LocalConfigurationResourceDao = SysResourceDao.Instance;


            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<TerminalServiceConfig>(SysResourceModel.Value);

            Config.Code = Code;
            Config.Name = Name;

            SaveCommand = new RelayCommand(_ => RunTerminalCommand(Save));
            RefreshCommand = new RelayCommand(_ => RunTerminalCommand(() =>
            {
                if (!TryReloadDevices())
                    MessageBox.Show(Application.Current.GetActiveWindow(), EngineLocalization.Get("未能提交终端设备重载请求。"), "ColorVision",
                        MessageBoxButton.OK, MessageBoxImage.Information);
            }), _ => ResourceDatabaseEndpoint?.IsCurrent == true && !IsConfigurationRetired);
            EditCommand = new RelayCommand(a =>
            {
                if (!ServiceAuthorization.Can("service.configure", SysResourceModel)) return;
                var target = CaptureConfigurationTarget();
                var nameConfig = new BaseConfig { Name = Name };
                PropertyEditorWindow window = new PropertyEditorWindow(nameConfig);
                window.Title = $"{Properties.Resources.MenuEdit} {Properties.Resources.ServiceName}";
                window.Owner = Application.Current.GetActiveWindow();
                window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                window.Submitting += (_, args) =>
                {
                    if (!ServicesHelper.IsInvalidPath(nameConfig.Name, Properties.Resources.ResourceName))
                    {
                        args.Cancel = true;
                        return;
                    }
                    try { Rename(nameConfig.Name, target); }
                    catch (Exception exception)
                    {
                        args.Cancel = true;
                        args.ErrorMessage = exception.Message;
                    }
                };
                window.ShowDialog();
            }, a => ServiceAuthorization.Can("service.configure", SysResourceModel));

            OpenCreateWindowCommand = new RelayCommand(a =>
            {
                if (!ServiceAuthorization.Can("device.create", SysResourceModel)) return;
                CreateTerminal createTerminal = new(this);
                createTerminal.Owner = Application.Current.GetActiveWindow();
                createTerminal.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                createTerminal.ShowDialog();
            }, a => ServiceAuthorization.Can("device.create", SysResourceModel));

            MQTTServiceTerminalBase = transport ?? new MQTTServiceTerminalBase<TerminalServiceConfig>(Config);

            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Create, Command = OpenCreateWindowCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuEdit, Command = EditCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = EngineLocalization.Get("重新加载终端设备"), Command = RefreshCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.MenuDelete, Command = DeleteCommand });
        }

        public override void Delete() => DeleteOwnedResource(DeleteWithConfirmation);

        internal bool DeleteWithConfirmation(Func<bool> confirm)
        {
            DemandDeleteAccess();
            var target = CaptureConfigurationTarget();
            var parent = Parent;
            bool IsCurrent() => target.IsCurrent(this);

            if (!IsCurrent() || !confirm()) return false;
            DemandDeleteAccess();
            if (!IsCurrent()) return false;

            var devices = VisualChildren.OfType<DeviceService>().Where(device => ReferenceEquals(device.Parent, this)).ToArray();
            if (!target.Resource.TryDelete(includeDirectChildren: true, IsCurrent)) return false;

            RetireConfiguration();
            foreach (var child in devices) child.CompleteResourceDeletion();
            try
            {
                parent?.RemoveChild(this);
                ServiceManager.Current?.TerminalServices.Remove(this);
            }
            catch (Exception error) { log.Warn("Terminal resources were deleted, but the service tree could not be updated.", error); }
            return true;
        }

        internal void RetireConfiguration()
        {
            if (Interlocked.Exchange(ref retired, 1) != 0) return;
            try { MQTTServiceTerminalBase.Dispose(); }
            catch (Exception error) { log.Warn("Could not dispose retired terminal transport.", error); }
        }

        public override UserControl GenDeviceControl() => new TerminalServiceControl(this);

        internal void Rename(string name, TerminalConfigurationTarget? expectedTarget = null)
        {
            ServiceAuthorization.Demand("service.configure", SysResourceModel);
            var target = expectedTarget ?? CaptureConfigurationTarget();
            SaveConfiguration(target, row =>
            {
                if (string.Equals(row.Name, name, StringComparison.Ordinal)) return;
                var storedConfig = string.IsNullOrWhiteSpace(row.Value) ? new JObject()
                    : JsonConvert.DeserializeObject<JObject>(row.Value, new JsonSerializerSettings { DateParseHandling = DateParseHandling.None }) ?? new JObject();
                storedConfig[nameof(BaseConfig.Name)] = name;
                row.Name = name;
                row.Value = storedConfig.ToString(Formatting.None);
            });
        }

        public override void Save()
        {
            var target = CaptureConfigurationTarget();
            var config = Config;
            SaveConfiguration(target, row =>
            {
                row.Code = config.Code;
                row.Name = config.Name;
                row.Value = JsonConvert.SerializeObject(config);
            });
        }

        private void SaveConfiguration(TerminalConfigurationTarget target, Action<SysResourceModel> update)
        {
            var saved = target.Resource.UpdateConfiguration(update, () => target.IsCurrent(this));
            if (saved == null)
                throw new InvalidOperationException(EngineLocalization.Get("终端或配置库已变更，请重新打开后再保存。"));
            if (!target.IsCurrent(this)) return;

            bool nameChanged = !string.Equals(SysResourceModel.Name, saved.Name, StringComparison.Ordinal);
            bool codeChanged = !string.Equals(SysResourceModel.Code, saved.Code, StringComparison.Ordinal);
            SysResourceModel.Code = saved.Code;
            SysResourceModel.Name = saved.Name;
            SysResourceModel.Value = saved.Value;
            Config.Name = Name;
            if (nameChanged) OnPropertyChanged(nameof(Name));
            if (codeChanged) OnPropertyChanged(nameof(Code));
        }

        internal bool TryReloadDevices(TerminalConfigurationTarget? expectedTarget = null)
        {
            var target = expectedTarget ?? CaptureConfigurationTarget();
            bool OwnsRequest() => target.IsCurrent(this);
            if (target.ReadRemoteRoute(this, includeDevices: true) is not { DeviceCodes.Count: > 0 } route) return false;
            return ReloadRequest is { } request ? request((route.TypeCode, route.ServiceCode), route.DeviceCodes, OwnsRequest)
                : MqttRCService.GetInstance().TryReloadServiceDevices(route.TypeCode, route.ServiceCode, route.DeviceCodes, OwnsRequest);
        }

        private static void RunTerminalCommand(Action command)
        {
            try { command(); }
            catch (Exception error)
            {
                log.Warn("Terminal command failed.", error);
                MessageBox.Show(Application.Current.GetActiveWindow(), error.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
