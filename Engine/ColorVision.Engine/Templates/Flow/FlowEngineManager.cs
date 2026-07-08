#pragma warning disable CA1822,CA1859,CS8602
using ColorVision.Common.MVVM;
using ColorVision.Engine.MQTT;
using ColorVision.Engine.Services.Flow;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.LogImp;
using FlowEngineLib;
using FlowEngineLib.Base;
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


    public class FlowEngineManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowEngineManager));

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
            ContextMenu.Items.Add(new MenuItem() { Header = "问 AI 分析当前流程", Command = AskCopilotFlowCommand });
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
            var contextItem = CopilotBusinessContextBuilder.BuildFlowContextItem(CaptureCopilotFlowSnapshot());
            var result = CopilotPromptRequestHelper.Dispatch(new CopilotPromptRequestOptions
            {
                Mode = CopilotPromptMode.Diagnose,
                Prompt = "请基于已附加的流程上下文，解释当前流程结构、关键节点参数、最近运行/失败信息，并给出优先排查建议。不要假设流程已经重新运行，只能使用快照中已有的信息。",
                StartNewConversation = true,
                SendNow = true,
                AttachContextSnapshot = true,
                ContextAttachmentTitle = contextItem.Title,
                ContextAttachmentSourceId = "flow-engine-manager",
                ContextItems = new[] { contextItem },
            });

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
            var batch = Batch;
            var recentRunMessage = View?.logTextBox?.Text ?? string.Empty;

            return new CopilotFlowContextSnapshot
            {
                SourceId = "flow-engine-manager",
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
                RecentFailureSummary = ExtractRecentFlowFailureSummary(recentRunMessage),
                Nodes = nodes,
            };
        }

        private IReadOnlyList<CopilotFlowNodeContextSnapshot> BuildNodeSnapshots()
        {
            var result = new List<CopilotFlowNodeContextSnapshot>();
            var nodeEditor = View?.STNodeEditorMain;
            if (nodeEditor?.Nodes == null)
                return result;

            foreach (STNode node in nodeEditor.Nodes)
            {
                result.Add(new CopilotFlowNodeContextSnapshot
                {
                    Title = node.Title ?? string.Empty,
                    NodeName = node is CVCommonNode commonNode ? commonNode.NodeName ?? string.Empty : string.Empty,
                    NodeType = node is CVCommonNode commonNode1 ? commonNode1.NodeType ?? string.Empty : node.GetType().Name,
                    DeviceCode = node is CVCommonNode commonNode2 ? commonNode2.DeviceCode ?? string.Empty : string.Empty,
                    NodeId = node is CVCommonNode commonNode3 ? commonNode3.NodeID ?? string.Empty : node.Guid.ToString(),
                    Position = $"Left={node.Left}, Top={node.Top}, Width={node.Width}, Height={node.Height}",
                    Mark = node.Mark ?? string.Empty,
                    IsActive = ReferenceEquals(nodeEditor.ActiveNode, node),
                    IsSelected = node.IsSelected,
                    Inputs = DescribeOptions(node.GetAllInputOptions()),
                    Outputs = DescribeOptions(node.GetAllOutputOptions()),
                    Parameters = BuildNodeParameterSummary(node),
                });
            }

            return result;
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

        private static string ExtractRecentFlowFailureSummary(string? recentRunMessage)
        {
            if (string.IsNullOrWhiteSpace(recentRunMessage))
                return string.Empty;

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

            return lines.Length == 0 ? string.Empty : string.Join(Environment.NewLine, lines);
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
