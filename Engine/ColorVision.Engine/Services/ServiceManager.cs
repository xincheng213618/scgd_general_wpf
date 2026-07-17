#pragma warning disable CS8625
using ColorVision.Database;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.PhyCameras.Group;
using ColorVision.Engine.Services.Terminal;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates.Flow;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Data;
using System.Linq;
using System.Threading;
using System.Windows;

namespace ColorVision.Engine.Services
{

    public class ServiceManager
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceManager));
        private static ServiceManager _instance;
        private static readonly object _locker = new();
        private readonly Dictionary<IDisPlayControl, DeviceService> _copilotDisplayDevices = new();
        private IDisposable? _copilotAgentExtensionRegistration;
        private DeviceService? _currentCopilotDisplayDevice;
        private string? _publishedCopilotDeviceSourceId;

        public static ServiceManager GetInstance()
        {
            ServiceManager instance;
            lock (_locker)
            {
                instance = _instance ??= new ServiceManager();
            }

            instance.EnsureCopilotAgentExtensionRegistered();
            return instance;
        }

        internal static ServiceManager? Current
        {
            get
            {
                lock (_locker)
                {
                    return _instance;
                }
            }
        }
        public ObservableCollection<TypeService> TypeServices { get; set; } = new ObservableCollection<TypeService>();
        public ObservableCollection<TerminalService> TerminalServices { get; set; } = new ObservableCollection<TerminalService>();
        public ObservableCollection<DeviceService> DeviceServices { get; set; } = new ObservableCollection<DeviceService>();

        public IEnumerable<DeviceService> GetImageSourceServices() => DeviceServices.Where(item => item is DeviceCamera || item is DeviceCalibration|| item is DeviceAlgorithm);

        public ObservableCollection<GroupResource> GroupResources { get; set; } = new ObservableCollection<GroupResource>();
        public ObservableCollection<DeviceService> LastGenControl { get; set; } = new ObservableCollection<DeviceService>();

        public event EventHandler ServiceChanged;

        public ServiceManager()
        {
            DisPlayManager.GetInstance().SelectedControlChanged += DisPlayManager_SelectedControlChanged;
            DeviceServices.CollectionChanged += DeviceServices_CollectionChanged;
            if (MySqlControl.GetInstance().IsConnect)
            {
                Application.Current.Dispatcher.Invoke(() => LoadServices());
            }
            MySqlControl.GetInstance().MySqlConnectChanged += (s, e) =>
                Application.Current.Dispatcher.Invoke(() => LoadServices());
        }

        public void GenControl(ObservableCollection<DeviceService> MQTTDevices)
        {
            LastGenControl = MQTTDevices;
            var nameToIndexMap = DisPlayManagerConfig.Instance.StoreIndex;

            ResetCopilotDisplayDeviceMap();
            DisPlayManager.GetInstance().IDisPlayControls.Clear();
            DisPlayManager.GetInstance().IDisPlayControls.Insert(0, FlowEngineManager.GetInstance().DisplayFlow);
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceService device)
                {
                    if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                    {
                        _copilotDisplayDevices[disPlayControl] = device;
                        DisPlayManager.GetInstance().IDisPlayControls.Add(disPlayControl);
                    }
                }
            }

            DisPlayManager.GetInstance().RestoreControl();
            DisPlayManager_SelectedControlChanged(this, EventArgs.Empty);
        }
        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
            LastGenControl = new ObservableCollection<DeviceService>();
            ResetCopilotDisplayDeviceMap();
            DisPlayManager.GetInstance().IDisPlayControls.Clear();
            DisPlayManager.GetInstance().IDisPlayControls.Insert(0, FlowEngineManager.GetInstance().DisplayFlow);
            foreach (var serviceKind in TypeServices)
            {
                foreach (var service in serviceKind.VisualChildren)
                {
                    foreach (var item in service.VisualChildren)  
                    {
                        if (item is DeviceService device)
                        {
                            LastGenControl.Add(device);
                            if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                            {
                                _copilotDisplayDevices[disPlayControl] = device;
                                DisPlayManager.GetInstance().IDisPlayControls.Add(disPlayControl);
                            }
                        }
                    }
                }
            }
            LastGenControl = DeviceServices;

            DisPlayManager.GetInstance().RestoreControl();
            DisPlayManager_SelectedControlChanged(this, EventArgs.Empty);
        }


        public void LoadServices()
        {
            ClearCurrentCopilotDeviceContext();
            ResetCopilotDisplayDeviceMap();
            LastGenControl?.Clear();
            List<SysDictionaryModel> SysDictionaryModels = SysDictionaryDao.Instance.GetAllByPid(1);

            TypeServices.Clear();
            foreach (var sysDictionaryModel in SysDictionaryModels)
            {
                if(sysDictionaryModel.Value == 6 || sysDictionaryModel.Value == 11 || sysDictionaryModel.Value == 12 || sysDictionaryModel.Value == 13 || sysDictionaryModel.Value == 14 || sysDictionaryModel.Value == 15 || sysDictionaryModel.Value == 16 || sysDictionaryModel.Value == 17)
                {
                    continue;
                }
                TypeService typeService = new();
                typeService.Name = sysDictionaryModel.Name ?? Properties.Resources.NotConfigured;
                typeService.SysDictionaryModel = sysDictionaryModel;
                TypeServices.Add(typeService);
            }


            TerminalServices.Clear();

            foreach (var typeService1 in TypeServices)
            {
                List<SysResourceModel> sysResourceModelServices = SysResourceDao.Instance.GetAllByParam(new Dictionary<string, object>() { { "type",(int)typeService1.ServiceTypes }, { "pid",null} ,{ "tenant_id", 0}, { "is_delete", 0} });
                foreach (var sysResourceModel in sysResourceModelServices)
                {
                    TerminalService terminalService = new TerminalService(sysResourceModel);
                    typeService1.AddChild(terminalService);
                    TerminalServices.Add(terminalService);
                }
            }

            DeviceServices.Clear();
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            foreach (var terminalService in TerminalServices)
            {
                var sysResourceModels = Db.Queryable<SysResourceModel>().Where(it => it.Pid == terminalService.SysResourceModel.Id && it.TenantId == 0 && it.IsEnable == true && it.IsDelete == false).ToList();
                foreach (var sysResourceModel in sysResourceModels)
                {
                    DeviceService? deviceService = DeviceServiceFactoryRegistry.CreateService(sysResourceModel);

                    if (deviceService != null  )
                    {
                        terminalService.AddChild(deviceService);
                        DeviceServices.Add(deviceService);
                    }
                }
            }

            GroupResources.Clear();


            foreach (var deviceService in DeviceServices)
            {
                List<SysResourceModel> sysResourceModels = Db.Queryable<SysResourceModel>().Where(it => it.Pid == deviceService.SysResourceModel.Id && it.IsDelete == false && it.IsEnable == true).ToList();
                foreach (var sysResourceModel in sysResourceModels)
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource groupResource = new(sysResourceModel);
                        deviceService.AddChild(groupResource);
                        GroupResources.Add(groupResource);
                    }
                   else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 50)
                    {
                        CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                    else
                    {
                        ServiceFileBase calibrationResource = new(sysResourceModel);
                        deviceService.AddChild(calibrationResource);
                    }
                }
            }

            foreach (var groupResource in GroupResources)
            {
                LoadgroupResource(groupResource);
            }
            ServiceChanged?.Invoke(this, new EventArgs());
        }

        internal bool HasCurrentCopilotDeviceSurface()
        {
            if (Volatile.Read(ref _currentCopilotDisplayDevice) != null)
                return true;

            return CopilotLiveContextRegistry.Current?.SourceId.StartsWith(
                CopilotDeviceContextFactory.SourceIdPrefix,
                StringComparison.Ordinal) == true;
        }

        internal CopilotBusinessContextBundle? CaptureCopilotDeviceContext(string userText)
        {
            DeviceService? device = FindMentionedDevice(userText)
                ?? FindDeviceBySourceId(CopilotLiveContextRegistry.Current?.SourceId)
                ?? GetCurrentDisplayDevice();
            if (device == null && DeviceServices.Count == 1)
                device = DeviceServices[0];
            if (device == null)
                return CopilotDeviceContextFactory.CaptureFleet(DeviceServices);
            if (!ContainsDevice(device))
                return null;

            return device.CaptureCopilotContext();
        }

        internal void PublishCurrentCopilotDisplayContext()
        {
            var device = GetCurrentDisplayDevice();
            if (device == null)
            {
                ClearPublishedCopilotDeviceContext();
                return;
            }

            try
            {
                var bundle = device.CaptureCopilotContext();
                CopilotBusinessContextCoordinator.Publish(bundle);
                _publishedCopilotDeviceSourceId = bundle.SourceId;
            }
            catch (Exception ex)
            {
                ClearPublishedCopilotDeviceContext();
                log.Debug("Could not publish the selected device as Copilot live context.", ex);
            }
        }

        private void EnsureCopilotAgentExtensionRegistered()
        {
            if (_copilotAgentExtensionRegistration != null)
                return;

            try
            {
                _copilotAgentExtensionRegistration = CopilotDeviceAgentExtension.Register(
                    CopilotAgentExtensionRegistry.Shared,
                    CopilotDeviceContextProvider.Create(this),
                    GetType().Assembly.GetName().Version?.ToString());
            }
            catch (Exception ex)
            {
                log.Warn("Could not register the Device Services Copilot Agent extension.", ex);
            }
        }

        private void DisPlayManager_SelectedControlChanged(object? sender, EventArgs e)
        {
            var selectedControl = DisPlayManager.GetInstance().SelectedControl;
            DeviceService? selectedDevice = null;
            if (selectedControl != null
                && _copilotDisplayDevices.TryGetValue(selectedControl, out var mappedDevice)
                && ContainsDevice(mappedDevice))
            {
                selectedDevice = mappedDevice;
            }

            Volatile.Write(ref _currentCopilotDisplayDevice, selectedDevice);
            PublishCurrentCopilotDisplayContext();
        }

        private void DeviceServices_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                ClearCurrentCopilotDeviceContext();
                ResetCopilotDisplayDeviceMap();
                return;
            }

            if (e.Action is not (NotifyCollectionChangedAction.Remove or NotifyCollectionChangedAction.Replace)
                || e.OldItems == null)
                return;
            foreach (var removedDevice in e.OldItems.OfType<DeviceService>())
                ForgetCopilotDevice(removedDevice);
        }

        private void ForgetCopilotDevice(DeviceService device)
        {
            foreach (var control in _copilotDisplayDevices
                .Where(entry => ReferenceEquals(entry.Value, device))
                .Select(entry => entry.Key)
                .ToArray())
            {
                _copilotDisplayDevices.Remove(control);
            }

            if (ReferenceEquals(Volatile.Read(ref _currentCopilotDisplayDevice), device))
                Volatile.Write(ref _currentCopilotDisplayDevice, null);

            var sourceId = CopilotDeviceContextFactory.GetSourceId(device);
            if (string.Equals(_publishedCopilotDeviceSourceId, sourceId, StringComparison.Ordinal))
                _publishedCopilotDeviceSourceId = null;
            CopilotLiveContextRegistry.Clear(sourceId);
        }

        private DeviceService? GetCurrentDisplayDevice()
        {
            var device = Volatile.Read(ref _currentCopilotDisplayDevice);
            return device != null && ContainsDevice(device) ? device : null;
        }

        private DeviceService? FindDeviceBySourceId(string? sourceId)
        {
            if (string.IsNullOrWhiteSpace(sourceId)
                || !sourceId.StartsWith(CopilotDeviceContextFactory.SourceIdPrefix, StringComparison.Ordinal))
            {
                return null;
            }

            return DeviceServices.FirstOrDefault(device => string.Equals(
                CopilotDeviceContextFactory.GetSourceId(device),
                sourceId,
                StringComparison.Ordinal));
        }

        private DeviceService? FindMentionedDevice(string? userText)
        {
            if (string.IsNullOrWhiteSpace(userText))
                return null;

            var matches = DeviceServices
                .Where(device => ContainsIdentifier(userText, device.Code) || ContainsIdentifier(userText, device.Name))
                .Take(2)
                .ToArray();
            return matches.Length == 1 ? matches[0] : null;
        }

        private bool ContainsDevice(DeviceService device)
        {
            return DeviceServices.Any(candidate => ReferenceEquals(candidate, device));
        }

        private static bool ContainsIdentifier(string userText, string? identifier)
        {
            var normalized = identifier?.Trim() ?? string.Empty;
            return normalized.Length >= 2 && userText.Contains(normalized, StringComparison.OrdinalIgnoreCase);
        }

        private void ResetCopilotDisplayDeviceMap()
        {
            _copilotDisplayDevices.Clear();
            Volatile.Write(ref _currentCopilotDisplayDevice, null);
            ClearPublishedCopilotDeviceContext();
        }

        private void ClearCurrentCopilotDeviceContext()
        {
            ClearPublishedCopilotDeviceContext();
            var currentSourceId = CopilotLiveContextRegistry.Current?.SourceId;
            if (currentSourceId?.StartsWith(CopilotDeviceContextFactory.SourceIdPrefix, StringComparison.Ordinal) == true)
                CopilotLiveContextRegistry.Clear(currentSourceId);
        }

        private void ClearPublishedCopilotDeviceContext()
        {
            var sourceId = _publishedCopilotDeviceSourceId;
            _publishedCopilotDeviceSourceId = null;
            if (!string.IsNullOrWhiteSpace(sourceId))
                CopilotLiveContextRegistry.Clear(sourceId);
        }

        public void LoadgroupResource(GroupResource groupResource)
        {
            List<SysResourceModel> sysResourceModels = SysResourceDao.Instance.GetGroupResourceItems(groupResource.SysResourceModel.Id);
            foreach (var sysResourceModel in sysResourceModels)
            {
                if (sysResourceModel.Type == (int)ServiceTypes.Group)
                {
                    GroupResource groupResource1 = new(sysResourceModel);
                    LoadgroupResource(groupResource1);
                    groupResource.AddChild(groupResource1);
                    GroupResources.Add(groupResource1);
                }
                else if (30<=sysResourceModel.Type && sysResourceModel.Type <= 50)
                {
                    CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
                else
                {
                    ServiceBase calibrationResource = new(sysResourceModel);
                    groupResource.AddChild(calibrationResource);
                }
            }
        }

        private static string GetServiceKey(string svrType, string svrCode)
        {
            return svrType + ":" + svrCode;
        }

    }
}
