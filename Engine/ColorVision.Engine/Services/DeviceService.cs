#pragma warning disable CA1859,CS8604,CS8631
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Cache;
using ColorVision.Engine.Messages;
using ColorVision.Engine.Services.Devices;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Services.Types;
using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
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
        [CommandDisplay("AskCopilot", Order = -4), BrowsableAttribute(false)]
        public RelayCommand AskCopilotDeviceCommand { get; set; }

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
            ResetCommand = new RelayCommand(a =>
            {
                MessageBoxResult result = MessageBox1.Show(WindowHelpers.GetActiveWindow(), $"{ColorVision.Engine.Properties.Resources.ConfirmReset} {Name}?", "ColorVision", MessageBoxButton.OKCancel);
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
                    MessageBox.Show("ColorVision.Engine.Properties.Resources.ImportException", "ColorVision");
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
            AskCopilotDeviceCommand = new RelayCommand(a => AskCopilotAboutDevice());

            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Delete, Command = DeleteCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Export, Command = ExportCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Import, Command = ImportCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "问 AI 分析设备状态", Command = AskCopilotDeviceCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = Properties.Resources.Property, Command = PropertyCommand });


            Config = ServiceObjectBaseExtensions.TryDeserializeConfig<T>(SysResourceModel.Value);

            Config.Code = SysResourceModel.Code ?? string.Empty;
            Config.Name = SysResourceModel.Name ?? string.Empty;
            UpdateFilecfgCommand = new RelayCommand(a => UpdateFilecfg(), a=> Config is IFileServerCfg);
        }

        private void AskCopilotAboutDevice()
        {
            var contextItem = CopilotBusinessContextBuilder.BuildDeviceContextItem(BuildCopilotDeviceSnapshot());
            var result = CopilotPromptRequestHelper.Dispatch(new CopilotPromptRequestOptions
            {
                Mode = CopilotPromptMode.Diagnose,
                Prompt = "请基于已附加的设备/服务上下文，分析当前设备状态、配置风险、心跳或日志线索，并给出优先排查建议。不要执行任何设备控制动作，只能根据快照判断。",
                StartNewConversation = true,
                SendNow = true,
                AttachContextSnapshot = true,
                ContextAttachmentTitle = contextItem.Title,
                ContextAttachmentSourceId = $"device-service:{Code}",
                ContextItems = new[] { contextItem },
            });

            if (!result.WasSent)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), result.StatusMessage, "ColorVision", MessageBoxButton.OK,
                    result.IsAvailable ? MessageBoxImage.Warning : MessageBoxImage.Information);
            }
        }

        private CopilotDeviceContextSnapshot BuildCopilotDeviceSnapshot()
        {
            var mqttService = GetMQTTService();
            var logSnapshot = CaptureRecentLogSnapshot();

            return new CopilotDeviceContextSnapshot
            {
                SourceId = $"device-service:{Code}",
                Title = $"设备服务 · {Name}",
                ServiceName = Name,
                ServiceCode = Code,
                ServiceType = ServiceTypes.ToString(),
                DeviceStatus = mqttService?.DeviceStatus.ToString() ?? string.Empty,
                HeartbeatTime = HeartbeatTime > 0 ? $"{HeartbeatTime} ms" : string.Empty,
                SendTopic = SendTopic,
                SubscribeTopic = SubscribeTopic,
                RuntimeProperties = BuildRuntimeProperties(mqttService),
                ConfigProperties = BuildObjectProperties(Config),
                RecentLogSummary = logSnapshot.Summary,
                RecentLogContent = logSnapshot.Content,
            };
        }

        private IReadOnlyList<CopilotContextProperty> BuildRuntimeProperties(MQTTServiceBase? mqttService)
        {
            var properties = new List<CopilotContextProperty>
            {
                new() { Name = "SysResourceId", Value = SysResourceModel?.Id.ToString() ?? string.Empty },
                new() { Name = "SysResourcePid", Value = SysResourceModel?.Pid.ToString() ?? string.Empty },
                new() { Name = "SysResourceType", Value = SysResourceModel?.Type.ToString() ?? string.Empty },
                new() { Name = "SysResourceRemark", Value = SysResourceModel?.Remark ?? string.Empty },
            };

            if (mqttService != null)
            {
                properties.Add(new CopilotContextProperty { Name = "MQTTServiceName", Value = mqttService.ServiceName ?? string.Empty });
                properties.Add(new CopilotContextProperty { Name = "MQTTDeviceCode", Value = mqttService.DeviceCode ?? string.Empty });
                properties.Add(new CopilotContextProperty { Name = "MQTTDeviceStatus", Value = mqttService.DeviceStatus.ToString() });
            }

            return properties;
        }

        private static IReadOnlyList<CopilotContextProperty> BuildObjectProperties(object? source)
        {
            if (source == null)
                return Array.Empty<CopilotContextProperty>();

            var properties = new List<CopilotContextProperty>();
            foreach (var property in source.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                try
                {
                    var value = property.GetValue(source);
                    if (!IsSimpleValue(value, property.PropertyType))
                        continue;

                    properties.Add(new CopilotContextProperty
                    {
                        Name = property.Name,
                        Value = FormatValue(value),
                    });
                }
                catch
                {
                }
            }

            return properties;
        }

        private (string Summary, string Content) CaptureRecentLogSnapshot()
        {
            try
            {
                var baseDir = ResolveServiceBaseDirectory();
                if (string.IsNullOrWhiteSpace(baseDir))
                    return ("未找到服务安装目录。", string.Empty);

                var logDir = Path.Combine(baseDir, "log");
                var prefix = ResolveLogFilePrefix();
                string? logPath = string.IsNullOrWhiteSpace(prefix)
                    ? LogFileHelper.GetLatestMainLogPath(baseDir)
                    : LogFileHelper.GetMostRecentLogFile(logDir, prefix);

                if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
                    logPath = Directory.Exists(logDir)
                        ? Directory.EnumerateFiles(logDir, "*.log", SearchOption.TopDirectoryOnly)
                            .Select(path => new FileInfo(path))
                            .OrderByDescending(file => file.LastWriteTimeUtc)
                            .FirstOrDefault()?.FullName
                        : null;

                if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
                    return ("未找到最近服务日志。", string.Empty);

                var lines = File.ReadLines(logPath).TakeLast(80).ToArray();
                var content = string.Join(Environment.NewLine, lines);
                if (content.Length > 6000)
                    content = content[^6000..];

                return ($"已读取最近服务日志：{Path.GetFileName(logPath)}，保留最后 {lines.Length} 行。", content);
            }
            catch (Exception ex)
            {
                return ($"读取最近服务日志失败：{ex.Message}", string.Empty);
            }
        }

        private string? ResolveServiceBaseDirectory()
        {
            string? servicePath = ServiceTypes == ColorVision.Engine.Services.Types.ServiceTypes.SMU
                ? ServiceConfig.Instance.CVMainService_dev
                : ServiceConfig.Instance.CVMainService_x64;

            if (string.IsNullOrWhiteSpace(servicePath))
                servicePath = ServiceConfig.Instance.CVMainService_x64Info?.ExecutablePath;

            try
            {
                return string.IsNullOrWhiteSpace(servicePath) ? null : Directory.GetParent(servicePath)?.FullName;
            }
            catch
            {
                return null;
            }
        }

        private string ResolveLogFilePrefix()
        {
            return ServiceTypes switch
            {
                ColorVision.Engine.Services.Types.ServiceTypes.Camera => "CVMainWindowsService_x64_camera",
                ColorVision.Engine.Services.Types.ServiceTypes.Spectrum => "CVMainWindowsService_x64_Spectrum",
                ColorVision.Engine.Services.Types.ServiceTypes.Algorithm => "CVMainWindowsService_x64_Algorithm",
                ColorVision.Engine.Services.Types.ServiceTypes.ThirdPartyAlgorithms => "CVMainWindowsService_x64_Algorithm",
                ColorVision.Engine.Services.Types.ServiceTypes.PG => "CVMainWindowsService_x64_CVOLED",
                ColorVision.Engine.Services.Types.ServiceTypes.SMU => "CVMainWindowsService_dev_SMU",
                _ => string.Empty,
            };
        }

        private static bool IsSimpleValue(object? value, Type propertyType)
        {
            if (value == null)
                return true;

            var source = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
            return source.IsPrimitive
                || source.IsEnum
                || source == typeof(string)
                || source == typeof(decimal)
                || source == typeof(DateTime)
                || source == typeof(TimeSpan)
                || source == typeof(Guid);
        }

        private static string FormatValue(object? value)
        {
            return value switch
            {
                null => string.Empty,
                DateTime dateTime => dateTime.ToString("yyyy-MM-dd HH:mm:ss"),
                _ => value.ToString() ?? string.Empty,
            };
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

        public event EventHandler ConfigChanged;

        public void SaveConfig()
        {
            SysResourceModel.Code = Config.Code;
            SysResourceModel.Name = Config.Name;
            SysResourceModel.Value = JsonConvert.SerializeObject(Config);
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            DB.Updateable(SysResourceModel).ExecuteCommand();
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
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            string TypeCode =DB.Queryable<SysDictionaryModel>().Where(x=>x.Pid ==1 && x.Value ==SysResourceModel.Type).First().Key;
            string PCode = DB.Queryable<SysResourceModel>().InSingle(SysResourceModel.Pid).Code;

            MqttRCService.GetInstance().RestartServices(TypeCode, PCode, Config.Code);
        }


        public override void Delete()
        {
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.AvoidDeletionUnlessNecessary, "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.Cancel) return;
            base.Delete();

            Parent.RemoveChild(this);

            //删除数据库
            if (SysResourceModel != null)
            {
                using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                DB.Deleteable<SysResourceModel>().Where(it => it.Id == SysResourceModel.Id).ExecuteCommand();

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
