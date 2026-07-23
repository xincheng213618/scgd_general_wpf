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
using System.Diagnostics;
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
            List<IDisPlayControl> displayControls = new() { FlowEngineManager.GetInstance().DisplayFlow };

            ResetCopilotDisplayDeviceMap();
            foreach (var item in MQTTDevices)
            {
                if (item is DeviceService device)
                {
                    if (device.GetDisplayControl() is IDisPlayControl disPlayControl)
                    {
                        _copilotDisplayDevices[disPlayControl] = device;
                        displayControls.Add(disPlayControl);
                    }
                }
            }

            DisPlayManager.GetInstance().ReplaceControls(displayControls);
            DisPlayManager_SelectedControlChanged(this, EventArgs.Empty);
        }
        /// <summary>
        /// 生成显示空间
        /// </summary>
        public void GenDeviceDisplayControl()
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            LastGenControl = new ObservableCollection<DeviceService>();
            List<IDisPlayControl> displayControls = new() { FlowEngineManager.GetInstance().DisplayFlow };
            ResetCopilotDisplayDeviceMap();
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
                                displayControls.Add(disPlayControl);
                            }
                        }
                    }
                }
            }
            LastGenControl = DeviceServices;
            long controlCreationMilliseconds = phaseStopwatch.ElapsedMilliseconds;

            phaseStopwatch.Restart();
            DisPlayManager.GetInstance().ReplaceControls(displayControls);
            long panelBuildMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            DisPlayManager_SelectedControlChanged(this, EventArgs.Empty);
            totalStopwatch.Stop();
            log.Info($"Device display controls generated. Controls={displayControls.Count}, " +
                $"Creation={controlCreationMilliseconds}ms, PanelBuild={panelBuildMilliseconds}ms, " +
                $"Total={totalStopwatch.ElapsedMilliseconds}ms.");
        }


        public void LoadServices()
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            ClearCurrentCopilotDeviceContext();
            ResetCopilotDisplayDeviceMap();
            LastGenControl?.Clear();
            Stopwatch phaseStopwatch = Stopwatch.StartNew();
            List<SysDictionaryModel> SysDictionaryModels = SysDictionaryDao.Instance.GetAllByPid(1);
            long dictionaryLoadMs = phaseStopwatch.ElapsedMilliseconds;
            ServiceResourceSnapshot resourceSnapshot = LoadServiceResourceSnapshot();

            phaseStopwatch.Restart();
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
                foreach (SysResourceModel sysResourceModel in resourceSnapshot.GetRootServices((int)typeService1.ServiceTypes))
                {
                    TerminalService terminalService = new TerminalService(sysResourceModel);
                    typeService1.AddChild(terminalService);
                    TerminalServices.Add(terminalService);
                }
            }
            long terminalBuildMs = phaseStopwatch.ElapsedMilliseconds;

            phaseStopwatch.Restart();
            Dictionary<ServiceTypes, (int Count, long Milliseconds)> deviceCreationStats = new();
            DeviceServices.Clear();

            foreach (var terminalService in TerminalServices)
            {
                foreach (SysResourceModel sysResourceModel in resourceSnapshot
                    .GetActiveChildren(terminalService.SysResourceModel.Id)
                    .Where(resource => resource.TenantId == 0))
                {
                    Stopwatch deviceStopwatch = Stopwatch.StartNew();
                    DeviceService? deviceService = DeviceServiceFactoryRegistry.CreateService(sysResourceModel);
                    deviceStopwatch.Stop();
                    ServiceTypes serviceType = (ServiceTypes)sysResourceModel.Type;
                    deviceCreationStats.TryGetValue(serviceType, out (int Count, long Milliseconds) currentStats);
                    deviceCreationStats[serviceType] = (currentStats.Count + 1, currentStats.Milliseconds + deviceStopwatch.ElapsedMilliseconds);

                    if (deviceService != null  )
                    {
                        terminalService.AddChild(deviceService);
                        DeviceServices.Add(deviceService);
                    }
                }
            }
            long deviceBuildMs = phaseStopwatch.ElapsedMilliseconds;

            phaseStopwatch.Restart();
            GroupResources.Clear();


            foreach (var deviceService in DeviceServices)
            {
                foreach (SysResourceModel sysResourceModel in resourceSnapshot.GetActiveChildren(deviceService.SysResourceModel.Id))
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

            foreach (GroupResource groupResource in GroupResources.ToArray())
            {
                LoadGroupResource(groupResource, resourceSnapshot, new HashSet<int>());
            }
            long childResourceBuildMs = phaseStopwatch.ElapsedMilliseconds;
            ServiceChanged?.Invoke(this, new EventArgs());
            totalStopwatch.Stop();
            string deviceTimings = string.Join(", ", deviceCreationStats
                .OrderByDescending(item => item.Value.Milliseconds)
                .Select(item => $"{item.Key}={item.Value.Count}/{item.Value.Milliseconds}ms"));
            log.Info($"Service hierarchy loaded. Types={TypeServices.Count}, Terminals={TerminalServices.Count}, Devices={DeviceServices.Count}, Groups={GroupResources.Count}, Dictionary={dictionaryLoadMs}ms, Terminals={terminalBuildMs}ms, Devices={deviceBuildMs}ms [{deviceTimings}], Children={childResourceBuildMs}ms, Total={totalStopwatch.ElapsedMilliseconds}ms.");
        }

        private static ServiceResourceSnapshot LoadServiceResourceSnapshot()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            using var db = new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true
            });

            List<SysResourceModel> rootServices = db.Queryable<SysResourceModel>()
                .Where(resource => resource.Pid == null && resource.TenantId == 0 && resource.IsDelete == false)
                .ToList();
            List<SysResourceModel> activeChildren = db.Queryable<SysResourceModel>()
                .Where(resource => resource.Pid != null && resource.IsEnable == true && resource.IsDelete == false)
                .ToList();
            List<SysResourceGoupModel> groupLinks = db.Queryable<SysResourceGoupModel>().ToList();
            int[] linkedResourceIds = groupLinks
                .Select(link => link.ResourceId)
                .Distinct()
                .ToArray();
            List<SysResourceModel> linkedResources = linkedResourceIds.Length == 0
                ? new List<SysResourceModel>()
                : db.Queryable<SysResourceModel>()
                    .Where(resource => linkedResourceIds.Contains(resource.Id))
                    .ToList();

            stopwatch.Stop();
            log.Info($"Service resource snapshot loaded. Roots={rootServices.Count}, ActiveChildren={activeChildren.Count}, GroupLinks={groupLinks.Count}, Queries=4, Took={stopwatch.ElapsedMilliseconds} ms.");
            return new ServiceResourceSnapshot(rootServices, activeChildren, groupLinks, linkedResources);
        }

        private void LoadGroupResource(
            GroupResource groupResource,
            ServiceResourceSnapshot snapshot,
            HashSet<int> ancestorGroupIds)
        {
            int groupId = groupResource.SysResourceModel.Id;
            if (!ancestorGroupIds.Add(groupId))
            {
                log.Warn($"Skip cyclic service group reference at resource {groupId}.");
                return;
            }

            try
            {
                foreach (SysResourceModel sysResourceModel in snapshot.GetGroupItems(groupId))
                {
                    if (sysResourceModel.Type == (int)ServiceTypes.Group)
                    {
                        GroupResource nestedGroup = new(sysResourceModel);
                        LoadGroupResource(nestedGroup, snapshot, ancestorGroupIds);
                        groupResource.AddChild(nestedGroup);
                        GroupResources.Add(nestedGroup);
                    }
                    else if (30 <= sysResourceModel.Type && sysResourceModel.Type <= 50)
                    {
                        CalibrationResource calibrationResource = CalibrationResource.EnsureInstance(sysResourceModel);
                        groupResource.AddChild(calibrationResource);
                    }
                    else
                    {
                        ServiceBase serviceResource = new(sysResourceModel);
                        groupResource.AddChild(serviceResource);
                    }
                }
            }
            finally
            {
                ancestorGroupIds.Remove(groupId);
            }
        }

        private sealed class ServiceResourceSnapshot
        {
            private readonly ILookup<int, SysResourceModel> _rootServicesByType;
            private readonly ILookup<int, SysResourceModel> _activeChildrenByParentId;
            private readonly Dictionary<int, IReadOnlyList<SysResourceModel>> _groupItemsByGroupId;

            public ServiceResourceSnapshot(
                IEnumerable<SysResourceModel> rootServices,
                IEnumerable<SysResourceModel> activeChildren,
                IEnumerable<SysResourceGoupModel> groupLinks,
                IEnumerable<SysResourceModel> linkedResources)
            {
                _rootServicesByType = rootServices.ToLookup(resource => resource.Type);
                _activeChildrenByParentId = activeChildren
                    .Where(resource => resource.Pid.HasValue)
                    .ToLookup(resource => resource.Pid!.Value);

                Dictionary<int, SysResourceModel> resourcesById = linkedResources
                    .GroupBy(resource => resource.Id)
                    .ToDictionary(group => group.Key, group => group.First());
                _groupItemsByGroupId = groupLinks
                    .GroupBy(link => link.GroupId)
                    .ToDictionary(
                        group => group.Key,
                        group => (IReadOnlyList<SysResourceModel>)group
                            .Where(link => resourcesById.ContainsKey(link.ResourceId))
                            .Select(link => resourcesById[link.ResourceId])
                            .ToArray());
            }

            public IEnumerable<SysResourceModel> GetRootServices(int serviceType) => _rootServicesByType[serviceType];

            public IEnumerable<SysResourceModel> GetActiveChildren(int parentId) => _activeChildrenByParentId[parentId];

            public IEnumerable<SysResourceModel> GetGroupItems(int groupId) =>
                _groupItemsByGroupId.TryGetValue(groupId, out IReadOnlyList<SysResourceModel>? resources)
                    ? resources
                    : Array.Empty<SysResourceModel>();
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
