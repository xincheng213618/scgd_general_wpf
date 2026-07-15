#pragma warning disable CA1822,CA1859,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Start;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ST.Library.UI.NodeEditor;

namespace ColorVision.Engine.Templates.Flow
{
    public class IInitializerFlow : IInitializer
    {
        public string Name => "Flow";
        public IEnumerable<string> Dependencies => Array.Empty<string>();
        public int Order => 10;

        public Task InitializeAsync()
        {
            MQTTConfig mQTTConfig = MQTTSetting.Instance.MQTTConfig;

            FlowEngineLib.MQTTHelper.SetDefaultCfg(mQTTConfig.Host, mQTTConfig.Port, mQTTConfig.UserName, mQTTConfig.UserPwd, false, null);
            return Task.CompletedTask;
        }
    }

    public class FlowEngineConfig : ViewModelBase, IConfig
    {
        public static FlowEngineConfig Instance => ConfigService.Instance.GetRequiredService<FlowEngineConfig>();

        [JsonIgnore]
        public RelayCommand EditCommand { get; set; }
        public FlowEngineConfig()
        {
            EditCommand = new RelayCommand(a => new PropertyEditorWindow(this) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
        }
        [Display(Name = "Engine_PG_EditSavePrompt", ResourceType = typeof(Properties.Resources))]
        public bool IsAutoEditSave { get => _IsAutoEditSave; set { _IsAutoEditSave = value; OnPropertyChanged(); } }
        private bool _IsAutoEditSave;

        public int LastSelectFlow { get => _LastSelectFlow; set { _LastSelectFlow = value; OnPropertyChanged(); } }
        private int _LastSelectFlow;

        public Dictionary<string, long> FlowRunTime { get; set; } = new Dictionary<string, long>();

        [Browsable(false)]
        public int TemplateFlowParamsIndex { get => _TemplateFlowParamsIndex; set { _TemplateFlowParamsIndex = value; OnPropertyChanged(); } }
        private int _TemplateFlowParamsIndex;

        [Browsable(false)]
        public int TemplateLargeFlowParamsIndex { get => _TemplateLargeFlowParamsIndex; set { _TemplateLargeFlowParamsIndex = value; OnPropertyChanged(); } }
        private int _TemplateLargeFlowParamsIndex;

    }


    public class FlowEngineManager : ViewModelBase, ICopilotBusinessContextSource
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowEngineManager));
        private string _copilotNodeCatalogSignature = string.Empty;
        private IReadOnlyList<CopilotFlowNodeTypeContextSnapshot> _copilotNodeCatalog = Array.Empty<CopilotFlowNodeTypeContextSnapshot>();

        private static FlowEngineManager _instance;
        private static readonly object _locker = new();
        public static FlowEngineManager? Current { get { lock (_locker) { return _instance; } } }
        public static FlowEngineManager GetInstance() { lock (_locker) { return _instance ??= new FlowEngineManager(); } }

        public FlowControl FlowControl { get; set; }

        public static FlowEngineConfig Config => FlowEngineConfig.Instance;
        public ObservableCollection<TemplateModel<FlowParam>> FlowParams { get; set; } = TemplateFlow.Params;

        public int TemplateFlowParamsIndex { get => Config.TemplateFlowParamsIndex; set { Config.TemplateFlowParamsIndex = value; OnPropertyChanged(); } }

       

        public ContextMenu ContextMenu { get; set; }
        public RelayCommand EditTemplateFlowCommand { get; set; }

        public RelayCommand MeasureBatchManagerCommand { get; set; }
        public ViewFlow View { get; set; }
        public FlowEngineControl FlowEngineControl { get; set; }

        public MeasureBatchModel Batch { get => _Batch; set { _Batch = value; OnPropertyChanged(); BatchRecord?.Invoke(this, _Batch); } }
        private MeasureBatchModel _Batch;
        public event EventHandler<MeasureBatchModel> BatchRecord;

        public FlowParam SlectFlowParam { get => _SlectFlowParam; set { _SlectFlowParam = value; OnPropertyChanged(); } }
        private FlowParam _SlectFlowParam;

        public double BatchProgress { get => _BatchProgress; set { _BatchProgress = value; OnPropertyChanged(); } }
        private double _BatchProgress ;

        public ServiceConfig ServiceConfig { get; set; }


        public ObservableCollection<CVBaseServerNode> CVBaseServerNodes { get; set; } = new ObservableCollection<CVBaseServerNode>();

        [DisplayName("OpenService")]
        public RelayCommand OpenServiceCommand { get; set; }

        public WindowsServiceBase WindowsServiceX64 { get; set; }
        public WindowsServiceBase WindowsServiceDev { get; set; }
        public WindowsServiceBase WindowsServiceReg { get; set; }
        public RelayCommand OpenCameraLogCommand { get; set; }
        public RelayCommand AskCopilotFlowCommand { get; set; }

        public Version ServiceVersion => new Version(ServiceConfig.RegistrationCenterServiceInfo.FileVersion ?? string.Empty);

        public FlowEngineManager()
        {
            ContextMenu = new ContextMenu();
            EditTemplateFlowCommand = new RelayCommand(a=> EditTemplateFlow());


            MeasureBatchManagerCommand = new RelayCommand(a=> MeasureBatchManager());

            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Inquire, Command = MeasureBatchManagerCommand });
            AskCopilotFlowCommand = new RelayCommand(a => AskCopilotAboutFlow());
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Flow_AskAiAnalyzeCurrentFlow, Command = AskCopilotFlowCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = ColorVision.Engine.Properties.Resources.Property, Command = Config.EditCommand });

            FlowEngineControl = new FlowEngineControl(false);

            View = new ViewFlow(this);

            FlowControl = new FlowControl(MQTTControl.GetInstance(), View.FlowEngineControl);

            ServiceConfig = ServiceConfig.Instance;
            OpenServiceCommand = new RelayCommand(a => ColorVision.Common.Utilities.PlatformHelper.OpenFolderAndSelectFile(ServiceConfig.RegistrationCenterService),a=>File.Exists(ServiceConfig.RegistrationCenterService));
            ContextMenu.Items.Add(new MenuItem() { Header = "OpenService", Command = OpenServiceCommand });
            WindowsServiceX64 = new WindowsServiceBase(ServiceConfig.CVMainService_x64Info);
            WindowsServiceDev = new WindowsServiceBase(ServiceConfig.CVMainService_devInfo);
            WindowsServiceReg = new WindowsServiceBase(ServiceConfig.RegistrationCenterServiceInfo);
            OpenCameraLogCommand = new RelayCommand(a => OpenCameraLog());

            DisplayFlow = new DisplayFlow(this);
        }

        public DisplayFlow DisplayFlow { get; set; }

        public void OpenCameraLog()
        {
            string baseDir = Directory.GetParent(ServiceConfig.CVMainService_x64).FullName;
            string latestLogPath = LogFileHelper.GetMostRecentLogFile(Path.Combine(baseDir,"log"), "CVMainWindowsService_x64_camera");
            if (!string.IsNullOrEmpty(latestLogPath))
            {
                WindowLogLocal windowLogLocal = new WindowLogLocal(latestLogPath, Encoding.GetEncoding("GB2312"));
                windowLogLocal.Show();
            }
        }

        private void AskCopilotAboutFlow()
        {
            var snapshot = CaptureCopilotFlowSnapshot();
            var contextItem = CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot);
            var bundle = CopilotBusinessContextBundle.FromItem(snapshot.SourceId, contextItem);
            var prompt = CopilotBusinessContextCoordinator.BuildFlowDiagnosisPrompt(snapshot, Properties.Resources.Flow_AiAnalyzeCurrentFlowPrompt);
            var result = CopilotBusinessContextCoordinator.DispatchDiagnosis(bundle, prompt);

            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK,
                    result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
        }

        public CopilotFlowContextSnapshot CaptureCopilotFlowSnapshot()
        {
            var flowParam = SlectFlowParam ?? (TemplateFlowParamsIndex >= 0 && TemplateFlowParamsIndex < FlowParams.Count ? FlowParams[TemplateFlowParamsIndex].Value : null);
            var nodes = BuildNodeSnapshots();
            var edges = BuildEdgeSnapshots();
            var batch = Batch;
            var recentRunMessage = View?.logTextBox?.Text ?? string.Empty;
            var failureEvidence = ExtractRecentFlowFailureEvidence(recentRunMessage);
            var focusedNodes = nodes.Where(node => node.IsSelected || node.IsActive).ToArray();

            return new CopilotFlowContextSnapshot
            {
                SourceId = "flow-engine-manager",
                Revision = ComputeFlowRevision(flowParam?.Id.ToString(), flowParam?.Name, View?.STNodeEditorMain?.Nodes.Cast<STNode>() ?? Enumerable.Empty<STNode>(), edges),
                FlowName = flowParam?.Name ?? string.Empty,
                TemplateName = flowParam?.Name ?? string.Empty,
                TemplateId = flowParam?.Id.ToString() ?? string.Empty,
                Status = FlowControl?.IsFlowRun == true ? "Running" : batch?.FlowStatus.ToString() ?? "Ready",
                IsRunning = FlowControl?.IsFlowRun == true,
                BatchSerialNumber = FirstNonEmpty(batch?.Code, batch?.Name),
                BatchStatus = batch?.FlowStatus.ToString() ?? string.Empty,
                BatchResult = batch?.Result ?? string.Empty,
                BatchProgress = $"{BatchProgress:0.##}%",
                LastNodeSummary = DisplayFlow?.LastNode?.ToShortString() ?? string.Empty,
                RecentRunMessage = recentRunMessage,
                RecentFailureSummary = string.Join(Environment.NewLine, failureEvidence),
                FocusedNodeSummary = string.Join(", ", focusedNodes.Select(node => FirstNonEmpty(node.Title, node.NodeName, node.NodeType, node.NodeId))),
                FailureEvidence = failureEvidence,
                Nodes = nodes,
                Edges = edges,
            };
        }

        public CopilotBusinessContextBundle CaptureCopilotContext()
        {
            var snapshot = CaptureCopilotFlowSnapshot();
            return CopilotBusinessContextBundle.FromItem(snapshot.SourceId, CopilotBusinessContextBuilder.BuildFlowContextItem(snapshot));
        }

        public CopilotFlowNodeCatalogSnapshot CaptureCopilotFlowNodeCatalog(string? query, int maxResults)
        {
            maxResults = Math.Clamp(maxResults, 1, 100);
            query = query?.Trim() ?? string.Empty;
            var catalog = GetCopilotFlowNodeCatalog();
            var matches = catalog
                .Where(nodeType => string.IsNullOrEmpty(query) || MatchesNodeType(nodeType, query))
                .OrderBy(nodeType => nodeType.CategoryPath, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(nodeType => nodeType.Title, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(nodeType => nodeType.RuntimeType, StringComparer.Ordinal)
                .ToArray();

            return new CopilotFlowNodeCatalogSnapshot
            {
                Query = query,
                TotalMatches = matches.Length,
                IsTruncated = matches.Length > maxResults,
                NodeTypes = matches.Take(maxResults).ToArray(),
            };
        }

        private IReadOnlyList<CopilotFlowNodeTypeContextSnapshot> GetCopilotFlowNodeCatalog()
        {
            var types = View?.STNodeEditorMain?.GetTypes() ?? Array.Empty<Type>();
            var signature = string.Join("\n", types.Select(GetNodeTypeKey).OrderBy(value => value, StringComparer.Ordinal));
            if (string.Equals(signature, _copilotNodeCatalogSignature, StringComparison.Ordinal))
                return _copilotNodeCatalog;

            var catalog = new List<CopilotFlowNodeTypeContextSnapshot>();
            foreach (var type in types.Where(type => type != null && !type.IsAbstract && typeof(STNode).IsAssignableFrom(type)))
            {
                try
                {
                    var attribute = type.GetCustomAttribute<STNodeAttribute>();
                    var node = Activator.CreateInstance(type) as STNode;
                    catalog.Add(new CopilotFlowNodeTypeContextSnapshot
                    {
                        TypeKey = GetNodeTypeKey(type),
                        RuntimeType = type.FullName ?? type.Name,
                        CategoryPath = attribute?.Path ?? string.Empty,
                        Title = node?.Title ?? type.Name,
                        Description = attribute?.DisplayDescription ?? string.Empty,
                        NodeType = node is CVCommonNode commonNode ? commonNode.NodeType ?? string.Empty : string.Empty,
                        DefaultDeviceCode = node is CVCommonNode commonNode1 ? commonNode1.DeviceCode ?? string.Empty : string.Empty,
                        Properties = BuildNodePropertySchemas(type),
                    });
                }
                catch (Exception ex)
                {
                    log.Debug($"Skip Copilot node catalog type {type.FullName}: {ex.Message}");
                }
            }

            _copilotNodeCatalogSignature = signature;
            _copilotNodeCatalog = catalog;
            return _copilotNodeCatalog;
        }

        private static IReadOnlyList<CopilotFlowNodePropertySchemaSnapshot> BuildNodePropertySchemas(Type type)
        {
            return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => (Property: property, Attribute: property.GetCustomAttribute<STNodePropertyAttribute>()))
                .Where(item => item.Attribute != null && !item.Attribute.IsHide && item.Property.GetIndexParameters().Length == 0)
                .Select(item => new CopilotFlowNodePropertySchemaSnapshot
                {
                    PropertyName = item.Property.Name,
                    DisplayName = string.IsNullOrWhiteSpace(item.Attribute!.Name) ? item.Property.Name : item.Attribute.Name,
                    Description = item.Attribute.Description ?? string.Empty,
                    DataType = item.Property.PropertyType.FullName ?? item.Property.PropertyType.Name,
                    IsWritable = item.Property.SetMethod?.IsPublic == true && !item.Attribute.IsReadOnly,
                })
                .OrderBy(property => property.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray();
        }

        private static bool MatchesNodeType(CopilotFlowNodeTypeContextSnapshot nodeType, string query)
        {
            return new[]
            {
                nodeType.Title,
                nodeType.Description,
                nodeType.CategoryPath,
                nodeType.NodeType,
                nodeType.DefaultDeviceCode,
                nodeType.RuntimeType,
                nodeType.TypeKey,
            }.Any(value => value.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        }

        private IReadOnlyList<CopilotFlowNodeContextSnapshot> BuildNodeSnapshots()
        {
            var result = new List<CopilotFlowNodeContextSnapshot>();
            var nodeEditor = View?.STNodeEditorMain;
            if (nodeEditor?.Nodes == null)
                return result;

            foreach (STNode node in nodeEditor.Nodes)
                result.Add(BuildNodeSnapshot(node, ReferenceEquals(nodeEditor.ActiveNode, node)));

            return result;
        }

        public CopilotFlowNodeContextSnapshot PreviewCopilotFlowNodeAddition(string typeKey, int left, int top, string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var node = CreateCopilotFlowNode(typeKey, left, top);
            return BuildNodeSnapshot(node, isActive: false);
        }

        public CopilotFlowContextSnapshot AddCopilotFlowNode(string typeKey, int left, int top, string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var editor = View?.STNodeEditorMain ?? throw new InvalidOperationException("No active Flow editor is available.");
            var node = CreateCopilotFlowNode(typeKey, left, top);
            try
            {
                if (node is CVBaseServerNode serverNode)
                {
                    var matchedService = MqttRCService.GetInstance().ServiceTokens.FirstOrDefault(service => service.Devices.Any(device => device.Key == serverNode.DeviceCode));
                    if (matchedService != null)
                        serverNode.Token = matchedService.Token;
                }
                else if (node is MQTTStartNode startNode)
                {
                    startNode.Server = MQTTControl.Config.Host;
                    startNode.Port = MQTTControl.Config.Port;
                }

                editor.Nodes.Add(node);
                return CaptureCopilotFlowSnapshot();
            }
            catch
            {
                if (editor.Nodes.Contains(node))
                    editor.Nodes.Remove(node);
                throw;
            }
        }

        public (CopilotFlowNodeContextSnapshot Node, string PropertyName, string OldValue, string NewValue) PreviewCopilotFlowNodePropertyChange(
            string nodeId,
            string propertyName,
            string value,
            string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var node = FindCopilotFlowNode(nodeId);
            var property = ResolveCopilotFlowProperty(node, propertyName);
            var clone = Activator.CreateInstance(node.GetType()) as STNode
                ?? throw new InvalidOperationException($"The Flow node type cannot be created: {GetNodeTypeKey(node.GetType())}");
            clone.Create();
            clone.OnLoadNode(ParseNodeSaveData(node.GetSaveData()));
            var oldValue = FormatFlowPropertyValue(property.GetValue(clone));
            clone.OnLoadNode(new Dictionary<string, byte[]> { [property.Name] = Encoding.UTF8.GetBytes(value ?? string.Empty) });
            return (BuildNodeSnapshot(clone, isActive: false), property.Name, oldValue, FormatFlowPropertyValue(property.GetValue(clone)));
        }

        public CopilotFlowContextSnapshot SetCopilotFlowNodeProperty(
            string nodeId,
            string propertyName,
            string value,
            string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var editor = View!.STNodeEditorMain;
            var node = FindCopilotFlowNode(nodeId);
            var property = ResolveCopilotFlowProperty(node, propertyName);
            var oldValue = property.GetValue(node);
            try
            {
                node.OnLoadNode(new Dictionary<string, byte[]> { [property.Name] = Encoding.UTF8.GetBytes(value ?? string.Empty) });
                editor.Invalidate();
                return CaptureCopilotFlowSnapshot();
            }
            catch
            {
                property.SetValue(node, oldValue);
                editor.Invalidate();
                throw;
            }
        }

        public CopilotFlowEdgeContextSnapshot PreviewCopilotFlowConnection(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId,
            string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var (output, input) = ResolveCopilotFlowConnection(sourceNodeId, sourcePortId, targetNodeId, targetPortId);
            EnsureCopilotFlowConnectionAllowed(output, input);
            return BuildEdgeSnapshot(output, input);
        }

        public CopilotFlowContextSnapshot ConnectCopilotFlowNodes(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId,
            string? expectedRevision)
        {
            EnsureCopilotFlowMutationAllowed(expectedRevision);
            var (output, input) = ResolveCopilotFlowConnection(sourceNodeId, sourcePortId, targetNodeId, targetPortId);
            EnsureCopilotFlowConnectionAllowed(output, input);
            var status = output.ConnectOption(input);
            if (status != ConnectionStatus.Connected)
                throw new InvalidOperationException($"Flow connection was rejected: {status}.");

            try
            {
                return CaptureCopilotFlowSnapshot();
            }
            catch
            {
                output.DisConnectOption(input);
                throw;
            }
        }

        private STNode FindCopilotFlowNode(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new InvalidOperationException("A stable Flow node instance id is required.");

            var node = View?.STNodeEditorMain?.Nodes.Cast<STNode>().FirstOrDefault(candidate =>
                string.Equals(candidate.Guid.ToString(), nodeId, StringComparison.OrdinalIgnoreCase)
                || candidate is CVCommonNode commonNode && string.Equals(commonNode.NodeID, nodeId, StringComparison.OrdinalIgnoreCase));
            return node ?? throw new InvalidOperationException($"The Flow node was not found: {nodeId}");
        }

        private static PropertyInfo ResolveCopilotFlowProperty(STNode node, string propertyName)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
                throw new InvalidOperationException("An exact Flow property name is required.");
            if (IsSensitiveFlowPropertyName(propertyName))
                throw new InvalidOperationException($"Copilot cannot read or change the sensitive Flow property: {propertyName}");

            var property = node.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                ?? throw new InvalidOperationException($"The Flow property was not found: {propertyName}");
            var attribute = property.GetCustomAttribute<STNodePropertyAttribute>();
            if (attribute == null || attribute.IsHide || attribute.IsReadOnly || property.SetMethod?.IsPublic != true)
                throw new InvalidOperationException($"The Flow property is not writable through Copilot: {propertyName}");
            return property;
        }

        private (STNodeOption Output, STNodeOption Input) ResolveCopilotFlowConnection(
            string sourceNodeId,
            string sourcePortId,
            string targetNodeId,
            string targetPortId)
        {
            var sourceNode = FindCopilotFlowNode(sourceNodeId);
            var targetNode = FindCopilotFlowNode(targetNodeId);
            var output = ResolveCopilotFlowPort(sourceNode.GetAllOutputOptions(), sourcePortId, "out");
            var input = ResolveCopilotFlowPort(targetNode.GetAllInputOptions(), targetPortId, "in");
            return (output, input);
        }

        private static STNodeOption ResolveCopilotFlowPort(STNodeOption[] options, string portId, string expectedDirection)
        {
            var prefix = expectedDirection + ":";
            if (string.IsNullOrWhiteSpace(portId)
                || !portId.StartsWith(prefix, StringComparison.Ordinal)
                || !int.TryParse(portId[prefix.Length..], out var index)
                || index < 0
                || index >= options.Length)
            {
                throw new InvalidOperationException($"The Flow port id is invalid or unavailable: {portId}");
            }

            return options[index] ?? throw new InvalidOperationException($"The Flow port is unavailable: {portId}");
        }

        private static void EnsureCopilotFlowConnectionAllowed(STNodeOption output, STNodeOption input)
        {
            var outputStatus = output.CanConnect(input);
            if (outputStatus != ConnectionStatus.Connected)
                throw new InvalidOperationException($"The source port cannot connect to the target port: {outputStatus}.");
            var inputStatus = input.CanConnect(output);
            if (inputStatus != ConnectionStatus.Connected)
                throw new InvalidOperationException($"The target port cannot accept the source port: {inputStatus}.");
        }

        private static CopilotFlowEdgeContextSnapshot BuildEdgeSnapshot(STNodeOption output, STNodeOption input)
        {
            return new CopilotFlowEdgeContextSnapshot
            {
                SourceNodeId = output.Owner.Guid.ToString(),
                SourcePortId = $"out:{Array.IndexOf(output.Owner.GetAllOutputOptions(), output)}",
                SourcePortName = output.Text ?? string.Empty,
                TargetNodeId = input.Owner.Guid.ToString(),
                TargetPortId = $"in:{Array.IndexOf(input.Owner.GetAllInputOptions(), input)}",
                TargetPortName = input.Text ?? string.Empty,
                DataType = output.DataType?.FullName ?? output.DataType?.Name ?? "System.Object",
            };
        }

        private static Dictionary<string, byte[]> ParseNodeSaveData(byte[] data)
        {
            var position = 0;
            var typeLength = data[position++];
            position += typeLength;
            var guidTypeLength = data[position++];
            position += guidTypeLength;
            var result = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            while (position < data.Length)
            {
                if (position + sizeof(int) > data.Length)
                    throw new InvalidDataException("The Flow node save data is incomplete.");
                var keyLength = BitConverter.ToInt32(data, position);
                position += sizeof(int);
                if (keyLength < 0 || position + keyLength + sizeof(int) > data.Length)
                    throw new InvalidDataException("The Flow node save data contains an invalid property name.");
                var key = Encoding.UTF8.GetString(data, position, keyLength);
                position += keyLength;
                var valueLength = BitConverter.ToInt32(data, position);
                position += sizeof(int);
                if (valueLength < 0 || position + valueLength > data.Length)
                    throw new InvalidDataException("The Flow node save data contains an invalid property value.");
                var value = new byte[valueLength];
                Array.Copy(data, position, value, 0, valueLength);
                position += valueLength;
                result[key] = value;
            }
            return result;
        }

        private static string FormatFlowPropertyValue(object? value)
        {
            if (value is Array array)
                return string.Join(",", array.Cast<object?>().Select(item => item?.ToString() ?? string.Empty));
            return value?.ToString() ?? string.Empty;
        }

        private static bool IsSensitiveFlowPropertyName(string propertyName)
        {
            return new[] { "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "accesskey", "privatekey", "license", "sn" }
                .Any(term => propertyName.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private void EnsureCopilotFlowMutationAllowed(string? expectedRevision)
        {
            if (FlowControl?.IsFlowRun == true)
                throw new InvalidOperationException("The active flow is running. Stop it before editing the graph.");
            if (View?.STNodeEditorMain == null)
                throw new InvalidOperationException("No active Flow editor is available.");

            if (!string.IsNullOrWhiteSpace(expectedRevision))
            {
                var currentRevision = CaptureCopilotFlowSnapshot().Revision;
                if (!string.Equals(currentRevision, expectedRevision, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"The flow changed after preview. Expected revision {expectedRevision}, current revision {currentRevision}.");
            }
        }

        private STNode CreateCopilotFlowNode(string typeKey, int left, int top)
        {
            if (string.IsNullOrWhiteSpace(typeKey))
                throw new InvalidOperationException("An exact Flow node type key is required.");
            if (left is < -100000 or > 100000 || top is < -100000 or > 100000)
                throw new InvalidOperationException("Flow node position must be between -100000 and 100000.");

            var type = View?.STNodeEditorMain?.GetTypes()
                .FirstOrDefault(candidate => string.Equals(GetNodeTypeKey(candidate), typeKey, StringComparison.Ordinal));
            if (type == null || type.IsAbstract || !typeof(STNode).IsAssignableFrom(type))
                throw new InvalidOperationException($"The Flow node type is not loaded: {typeKey}");

            var node = Activator.CreateInstance(type) as STNode
                ?? throw new InvalidOperationException($"The Flow node type cannot be created: {typeKey}");
            node.Create();
            node.Left = left;
            node.Top = top;
            return node;
        }

        private static CopilotFlowNodeContextSnapshot BuildNodeSnapshot(STNode node, bool isActive)
        {
            var runtimeType = node.GetType();
            var instanceId = node.Guid.ToString();
            return new CopilotFlowNodeContextSnapshot
            {
                InstanceId = instanceId,
                TypeKey = GetNodeTypeKey(runtimeType),
                RuntimeType = runtimeType.FullName ?? runtimeType.Name,
                CategoryPath = runtimeType.GetCustomAttribute<STNodeAttribute>()?.Path ?? string.Empty,
                Title = node.Title ?? string.Empty,
                NodeName = node is CVCommonNode commonNode ? commonNode.NodeName ?? string.Empty : string.Empty,
                NodeType = node is CVCommonNode commonNode1 ? commonNode1.NodeType ?? string.Empty : runtimeType.Name,
                DeviceCode = node is CVCommonNode commonNode2 ? commonNode2.DeviceCode ?? string.Empty : string.Empty,
                NodeId = node is CVCommonNode commonNode3 ? commonNode3.NodeID ?? instanceId : instanceId,
                Position = $"Left={node.Left}, Top={node.Top}, Width={node.Width}, Height={node.Height}",
                Left = node.Left,
                Top = node.Top,
                Width = node.Width,
                Height = node.Height,
                Mark = node.Mark ?? string.Empty,
                IsActive = isActive,
                IsSelected = node.IsSelected,
                Inputs = DescribeOptions(node.GetAllInputOptions()),
                Outputs = DescribeOptions(node.GetAllOutputOptions()),
                InputPorts = BuildPortSnapshots(node.GetAllInputOptions(), "in"),
                OutputPorts = BuildPortSnapshots(node.GetAllOutputOptions(), "out"),
                Parameters = BuildNodeParameterSummary(node),
            };
        }

        private IReadOnlyList<CopilotFlowEdgeContextSnapshot> BuildEdgeSnapshots()
        {
            var editor = View?.STNodeEditorMain;
            if (editor == null)
                return Array.Empty<CopilotFlowEdgeContextSnapshot>();

            return editor.GetConnectionInfo()
                .Where(connection => connection.Output?.Owner != null && connection.Input?.Owner != null)
                .Select(connection =>
                {
                    var outputOptions = connection.Output.Owner.GetAllOutputOptions();
                    var inputOptions = connection.Input.Owner.GetAllInputOptions();
                    return new CopilotFlowEdgeContextSnapshot
                    {
                        SourceNodeId = connection.Output.Owner.Guid.ToString(),
                        SourcePortId = $"out:{Array.IndexOf(outputOptions, connection.Output)}",
                        SourcePortName = connection.Output.Text ?? string.Empty,
                        TargetNodeId = connection.Input.Owner.Guid.ToString(),
                        TargetPortId = $"in:{Array.IndexOf(inputOptions, connection.Input)}",
                        TargetPortName = connection.Input.Text ?? string.Empty,
                        DataType = connection.Output.DataType?.FullName ?? connection.Output.DataType?.Name ?? "System.Object",
                    };
                })
                .OrderBy(edge => edge.SourceNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.SourcePortId, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetNodeId, StringComparer.Ordinal)
                .ThenBy(edge => edge.TargetPortId, StringComparer.Ordinal)
                .ToArray();
        }

        private static IReadOnlyList<CopilotFlowPortContextSnapshot> BuildPortSnapshots(STNodeOption[]? options, string direction)
        {
            if (options == null || options.Length == 0)
                return Array.Empty<CopilotFlowPortContextSnapshot>();

            return options.Select((option, index) => new CopilotFlowPortContextSnapshot
            {
                PortId = $"{direction}:{index}",
                Name = option?.Text ?? string.Empty,
                DataType = option?.DataType?.FullName ?? option?.DataType?.Name ?? "System.Object",
                IsSingle = option?.IsSingle == true,
                ConnectionCount = option?.ConnectionCount ?? 0,
            }).ToArray();
        }

        private static string GetNodeTypeKey(Type type)
        {
            return $"{type.Module.Name}|{type.FullName ?? type.Name}";
        }

        private static string ComputeFlowRevision(
            string? templateId,
            string? flowName,
            IEnumerable<STNode> nodes,
            IReadOnlyList<CopilotFlowEdgeContextSnapshot> edges)
        {
            var canonical = new StringBuilder();
            AppendCanonical(canonical, templateId);
            AppendCanonical(canonical, flowName);

            foreach (var node in nodes.OrderBy(node => node.Guid))
            {
                AppendCanonical(canonical, node.Guid.ToString());
                AppendCanonical(canonical, GetNodeTypeKey(node.GetType()));
                AppendCanonical(canonical, ComputeNodeStateHash(node));
            }

            foreach (var edge in edges)
            {
                AppendCanonical(canonical, edge.SourceNodeId);
                AppendCanonical(canonical, edge.SourcePortId);
                AppendCanonical(canonical, edge.TargetNodeId);
                AppendCanonical(canonical, edge.TargetPortId);
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
        }

        private static string ComputeNodeStateHash(STNode node)
        {
            try
            {
                return Convert.ToHexString(SHA256.HashData(node.GetSaveData()));
            }
            catch
            {
                var fallback = $"{node.Guid}|{GetNodeTypeKey(node.GetType())}|{node.Left}|{node.Top}|{node.Width}|{node.Height}|{node.Mark}";
                return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fallback)));
            }
        }

        private static void AppendCanonical(StringBuilder builder, string? value)
        {
            value ??= string.Empty;
            builder.Append(value.Length).Append(':').Append(value).Append(';');
        }

        private static IReadOnlyList<string> DescribeOptions(STNodeOption[]? options)
        {
            if (options == null || options.Length == 0)
                return Array.Empty<string>();

            return options
                .Where(option => option != null)
                .Select(option => $"{option.Text}({option.DataType?.Name ?? "object"}, connections {option.ConnectionCount})")
                .ToArray();
        }

        private static IReadOnlyList<string> ExtractRecentFlowFailureEvidence(string? recentRunMessage)
        {
            if (string.IsNullOrWhiteSpace(recentRunMessage))
                return Array.Empty<string>();

            var failureTerms = new[]
            {
                "fail", "failed", "failure", "error", "exception", "timeout",
                "失败", "错误", "异常", "超时",
            };

            var lines = recentRunMessage
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Reverse()
                .Where(line => failureTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .Take(8)
                .Reverse()
                .ToArray();

            return lines;
        }

        private static IReadOnlyList<CopilotContextProperty> BuildNodeParameterSummary(STNode node)
        {
            var properties = new List<CopilotContextProperty>();
            foreach (var property in node.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                var nodeProperty = property.GetCustomAttribute<STNodePropertyAttribute>();
                if (nodeProperty == null || nodeProperty.IsHide)
                    continue;

                if (!IsSimpleType(property.PropertyType))
                    continue;

                try
                {
                    var value = property.GetValue(node);
                    properties.Add(new CopilotContextProperty
                    {
                        Name = string.IsNullOrWhiteSpace(nodeProperty.Name) ? property.Name : nodeProperty.Name,
                        Value = value?.ToString() ?? string.Empty,
                    });
                }
                catch
                {
                }
            }

            return properties;
        }

        private static bool IsSimpleType(Type type)
        {
            var source = Nullable.GetUnderlyingType(type) ?? type;
            return source.IsPrimitive
                || source.IsEnum
                || source == typeof(string)
                || source == typeof(decimal)
                || source == typeof(DateTime)
                || source == typeof(TimeSpan)
                || source == typeof(Guid);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }


        public void MeasureBatchManager()
        {
            Frame frame = new Frame();

            MeasureBatchManagerPage batchDataHistory = new MeasureBatchManagerPage(frame);
            frame.Navigate(batchDataHistory);

            Window window = new Window() { Title = ColorVision.Engine.Properties.Resources.Inquire, Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner, Height = 720, Width = 1280 };
            window.Content = frame;
            window.Show();
        }

        public void EditTemplateFlow()
        {
            if (TemplateFlowParamsIndex < 0 || TemplateFlowParamsIndex >= FlowParams.Count)
                return;
            new TemplateEditorWindow(new TemplateFlow(), TemplateFlowParamsIndex) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
            _ = View.DisplayFlow.Refresh();
        }
    }
}
