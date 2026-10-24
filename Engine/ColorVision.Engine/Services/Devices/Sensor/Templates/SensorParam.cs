#pragma warning disable 
using ColorVision.Common.MVVM;
using ColorVision.Database;
using ColorVision.Engine.Templates;
using log4net;
using log4net.Util;
using MQTTMessageLib.Sensor;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;

namespace ColorVision.Engine.Services.Devices.Sensor.Templates
{
    public class TemplateSensor : ITemplate<SensorParam>, ITemplateCreatePresetProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateSensor));

        public static Dictionary<string, ObservableCollection<TemplateModel<SensorParam>>> Params { get; set; } = new Dictionary<string, ObservableCollection<TemplateModel<SensorParam>>>();

        public static ObservableCollection<TemplateModel<SensorParam>> AllParams { get => new(Params.SelectMany(p => p.Value)); }

        public TemplateSensor(string code)
        {
            Code = code;
            try
            {
                var model = SysDictionaryModMasterDao.Instance.GetByCode(code);
                if (model != null && model.Id>=0)
                {
                    TemplateDicId = model.Id;

                }
                else
                {
                    log.Info("找不到传感器code 对应的字典，正在添加字典");
                    SysDictionaryModModel sysDictionaryModModel = new SysDictionaryModModel() { Code = code, Name = code, ModType = 5 ,CreateDate =DateTime.Now};
                    SysDictionaryModMasterDao.Instance.Save(sysDictionaryModModel);
                    TemplateDicId = sysDictionaryModModel.Id;

                }

            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            if (Params.TryGetValue(Code, out var templatesParams))
            {
                TemplateParams = templatesParams;
            }
            else
            {
                templatesParams = new ObservableCollection<TemplateModel<SensorParam>>();
                TemplateParams = templatesParams;
                Params.Add(Code, templatesParams);
            }

            IsUserControl = true;
        }

        public override IEnumerable<TemplateCreatePreset> GetCreatePresets() => Code == "Sensor.Default"
            ? SensorTemplatePresets.All.Select(preset => preset.Source)
            : Enumerable.Empty<TemplateCreatePreset>();

        public override bool ApplyCreatePreset(string presetId)
        {
            if (Code != "Sensor.Default") return false;
            SensorTemplatePreset? preset = SensorTemplatePresets.Find(presetId);
            if (preset == null) return false;
            ImportTemp = SensorTemplatePresets.CreateDraft(preset: preset);
            CreateTemp = null;
            ImportName = preset.Source.SuggestedName;
            return true;
        }

        public override object CreateDefault()
        {
            if (ImportTemp != null) return CreateTemp = SensorTemplatePresets.CreateDraft(ImportTemp);
            var draft = new SensorParam();
            // Read custom category defaults without creating dictionary rows while previewing.
            foreach (var definition in SysDictionaryModDetailDao.Instance.GetAllByPid(TemplateDicId))
                draft.ModDetailModels.Add(new ModDetailModel { SysPid = definition.Id, Pid = -1, ValueA = definition.DefaultValue });
            return CreateTemp = draft.ModDetailModels.Count > 0 ? draft : SensorTemplatePresets.CreateDraft();
        }

        public override UserControl CreateUserControl() => new SensorTemplateCreatePreview();

        public override bool CopyTo(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return false;
            ImportTemp = SensorTemplatePresets.CreateDraft(TemplateParams[index].Value);
            CreateTemp = null;
            ImportName = TemplateParams[index].Key;
            return true;
        }

        public override bool ImportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;
            SensorParam? source = JsonConvert.DeserializeObject<SensorParam>(File.ReadAllText(filePath));
            if (source == null || source.ModDetailModels.Count == 0) return false;
            ImportTemp = SensorTemplatePresets.CreateDraft(source);
            CreateTemp = null;
            ImportName = Path.GetFileNameWithoutExtension(filePath);
            return true;
        }
        //这里创建模板的时候默认不启用
        public override void Create(string templateName)
        {
            SensorParam? AddParamMode()
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

                var (modMaster, definitions) = InsertTemplate(Db, TemplateDicId, templateName, CreateTemp ?? ImportTemp);
                foreach (var definition in definitions) SymbolCache.Instance.Cache.TryAdd(definition.Id, definition);

                if (modMaster.Id > 0)
                {
                    ModMasterModel modMasterModel = Db.Queryable<ModMasterModel>().InSingle(modMaster.Id);
                    var modDetailModels = Db.Queryable<ModDetailModel>().Where(it => it.Pid == modMaster.Id).OrderBy(it => it.Id).ToList();
                    if (modMasterModel != null)
                        return (SensorParam)Activator.CreateInstance(typeof(SensorParam), new object[] { modMasterModel, modDetailModels });
                }
                return null;
            }
            SensorParam? param = AddParamMode();
            if (ImportTemp != null) ImportTemp = null;
            if (param != null)
            {
                var a = new TemplateModel<SensorParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.CreateDataBase+$" {typeof(SensorParam)} "+ ColorVision.Engine.Properties.Resources.TemplateFailed, "ColorVision");
            }
        }

        internal static void EnsureDefaultCommandDefinition(int templateDicId)
        {
            SensorTemplateDictionaryService.GetOrCreateCommandDefinitions(templateDicId);
        }

        internal static (ModMasterModel Master, List<SysDictionaryModDetaiModel> Definitions) InsertTemplate(SqlSugarClient db, int dictionaryId, string name, SensorParam? draft)
        {
            if (dictionaryId <= 0) throw new InvalidOperationException("找不到传感器模板类型。");
            var master = new ModMasterModel { Pid = dictionaryId, Name = name, TenantId = 0 };
            db.Ado.BeginTran();
            try
            {
                master.Id = db.Insertable(master).ExecuteReturnIdentity();
                var definitions = SensorTemplateDictionaryService.GetOrCreateCommandDefinitions(db, dictionaryId);
                db.Insertable(BuildCreationDetails(draft, master.Id, definitions)).ExecuteCommand();
                db.Ado.CommitTran();
                return (master, definitions);
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
        }

        internal static List<ModDetailModel> BuildCreationDetails(SensorParam? draft, int modMasterId, IReadOnlyList<SysDictionaryModDetaiModel> definitions)
        {
            if (definitions.Count == 0) throw new InvalidOperationException("找不到传感器指令定义。");
            if (draft == null)
                return definitions.Select(item => new ModDetailModel { SysPid = item.Id, Pid = modMasterId, ValueA = item.DefaultValue }).ToList();
            if (draft.ModDetailModels.Count == 0) throw new InvalidOperationException("请至少添加一条传感器指令。");
            var definitionIds = definitions.Select(item => item.Id).ToHashSet();
            // Commands can share one definition. Copy every row, detach source IDs, and remap
            // imported/portable commands to a definition belonging to the destination type.
            return draft.ModDetailModels.Select(item => new ModDetailModel
            {
                SysPid = definitionIds.Contains(item.SysPid) ? item.SysPid : definitions[0].Id,
                Pid = modMasterId,
                ValueA = item.ValueA,
                ValueB = item.ValueB,
                IsEnable = item.IsEnable,
                IsDelete = item.IsDelete
            }).ToList();
        }

        public override string Title { get => Code + Properties.Resources.Edit; set { } }

        public EditTemplateSensor EditTemplateSensor { get; set; } = new EditTemplateSensor();

        public override UserControl GetUserControl() => EditTemplateSensor;

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateSensor.SetParam(TemplateParams[index].Value);
        }
        public override void Save()
        {
            if (SaveIndex.Count == 0) return;
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];

                    item.Value.ModMaster.Name = item.Value.Name;
                    Db.Updateable(item.Value.ModMaster).ExecuteCommand();


                    Db.Updateable(item.Value.ModDetailModels.ToList()).ExecuteCommand();

                }
            }
        }
    }

    public class SensorCommand : ViewModelBase
    {
        public ModDetailModel Model { get; set; } = new ModDetailModel();
        public SensorCommand(ModDetailModel modDetailModel)
        {
            Model = modDetailModel ?? new ModDetailModel();
            BracketTextToRequestCommand = new RelayCommand(_ => ApplyBracketTextToRequest());
            BracketTextToResponseCommand = new RelayCommand(_ => ApplyBracketTextToResponse());
            RequestToBracketTextCommand = new RelayCommand(_ => ShowRequestAsBracketText());
            ResponseToBracketTextCommand = new RelayCommand(_ => ShowResponseAsBracketText());
            ConvertEncodingCommand = new RelayCommand(target => { if (target is SensorCmdType type) TryConvertEncoding(type); });
            ParseRequestString();
        }

        public RelayCommand BracketTextToRequestCommand { get; }
        public RelayCommand BracketTextToResponseCommand { get; }
        public RelayCommand RequestToBracketTextCommand { get; }
        public RelayCommand ResponseToBracketTextCommand { get; }
        public RelayCommand ConvertEncodingCommand { get; }


        public void ParseRequestString()
        {
            string? str = Model.ValueA;
            if (string.IsNullOrEmpty(str))
            {
                GenerateRequestString();
            }
            else if (TryParseStoredCommand(str, out string request, out string response, out SensorCmdType cmdType, out int timeout, out int delay, out int retryCount))
            {
                _isLoading = true;
                Request = request;
                Response = response;
                SensorCmdType = cmdType;
                Timeout = timeout;
                Delay = delay;
                RetryCount = retryCount;
                _isLoading = false;
                RefreshPreviewProperties();
            }
        }


        public void GenerateRequestString()
        {
            if (_isLoading)
            {
                return;
            }

            _Request = _Request.Replace("\\r\\n", "\r\n")
                 .Replace("\\n", "\n")
                 .Replace("\\r", "\r")
                 .Replace("\\t", "\t");
            Model.ValueA = $"{_Request},{_Response},{SensorCmdType},{Timeout}/{Delay},{RetryCount}";
            RefreshPreviewProperties();
        }

        public string? OriginText { get => Model.ValueA; set { } }

        public string BracketText { get => _BracketText; set { _BracketText = value ?? string.Empty; OnPropertyChanged(); } }
        private string _BracketText = string.Empty;

        public string ConvertStatus { get => _ConvertStatus; set { _ConvertStatus = value; OnPropertyChanged(); } }
        private string _ConvertStatus = string.Empty;

        public string RequestBracketText => SensorCmdType == SensorCmdType.Hex ? SensorCommandTextFormatter.ToBracketTextOrOriginal(Request, EditTemplateSensor.Config.UseControlNamesInBracketText) : Request;
        public string ResponseBracketText => SensorCmdType == SensorCmdType.Hex ? SensorCommandTextFormatter.ToBracketTextOrOriginal(Response, EditTemplateSensor.Config.UseControlNamesInBracketText) : Response;
        public string RequestReadableText => SensorCommandTextFormatter.ToReadableText(Request, SensorCmdType == SensorCmdType.Hex);
        public string ResponseReadableText => SensorCommandTextFormatter.ToReadableText(Response, SensorCmdType == SensorCmdType.Hex);
        public string PreviewText => string.Format(Properties.Resources.Sensor_PreviewFormat, OriginText, RequestBracketText, ResponseBracketText);

        public SensorCmdType SensorCmdType { get => _SensorCmdType; set { _SensorCmdType = value; OnPropertyChanged(); GenerateRequestString(); } }
        private SensorCmdType _SensorCmdType = SensorCmdType.Ascii;

        public string Request { get => _Request; set { _Request = value; OnPropertyChanged(); GenerateRequestString(); } }
        private string _Request = string.Empty;
        public string Response { get => _Response; set { _Response = value; OnPropertyChanged(); GenerateRequestString(); } }
        private string _Response = string.Empty;

        public int Timeout { get => _Timeout; set { _Timeout = value; OnPropertyChanged(); GenerateRequestString(); } }
        private int _Timeout = 1000;

        public int Delay { get => _Delay; set { _Delay = value; OnPropertyChanged(); GenerateRequestString(); } }
        private int _Delay;

        public int RetryCount { get => _RetryCount; set { _RetryCount = value; OnPropertyChanged(); GenerateRequestString(); } }
        private int _RetryCount;

        private bool _isLoading;

        public bool TryConvertEncoding(SensorCmdType target)
        {
            try
            {
                string request = SensorCommandTextFormatter.ConvertEncoding(Request, SensorCmdType, target);
                string response = SensorCommandTextFormatter.ConvertEncoding(Response, SensorCmdType, target);
                // The legacy service splits ValueA on commas; the editor expands request escapes.
                if (target != SensorCmdType.Hex && (request.Contains(',') || response.Contains(',') ||
                    request.Contains("\\r") || request.Contains("\\n") || request.Contains("\\t")))
                {
                    ConvertStatus = Properties.Resources.Sensor_ConversionKeepHex;
                    return false;
                }
                _isLoading = true;
                try
                {
                    Request = request;
                    Response = response;
                    SensorCmdType = target;
                }
                finally { _isLoading = false; }
                GenerateRequestString();
                ConvertStatus = string.Format(Properties.Resources.Sensor_ConversionSuccess, target);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                ConvertStatus = string.Format(Properties.Resources.Sensor_ConversionFailed, target);
                return false;
            }
        }

        private void ApplyBracketTextToRequest()
        {
            if (!TryConvertEncoding(SensorCmdType.Hex)) return;
            Request = SensorCommandTextFormatter.BracketTextToHex(BracketText);
            ConvertStatus = Properties.Resources.Sensor_ConvertedToRequest;
        }

        private void ApplyBracketTextToResponse()
        {
            if (!TryConvertEncoding(SensorCmdType.Hex)) return;
            Response = SensorCommandTextFormatter.BracketTextToHex(BracketText);
            ConvertStatus = Properties.Resources.Sensor_ConvertedToResponse;
        }

        private void ShowRequestAsBracketText()
        {
            BracketText = RequestBracketText;
            ConvertStatus = Properties.Resources.Sensor_RequestEchoed;
        }

        private void ShowResponseAsBracketText()
        {
            BracketText = ResponseBracketText;
            ConvertStatus = Properties.Resources.Sensor_ResponseEchoed;
        }

        public void RefreshPreviewProperties()
        {
            OnPropertyChanged(nameof(OriginText));
            OnPropertyChanged(nameof(RequestBracketText));
            OnPropertyChanged(nameof(ResponseBracketText));
            OnPropertyChanged(nameof(RequestReadableText));
            OnPropertyChanged(nameof(ResponseReadableText));
            OnPropertyChanged(nameof(PreviewText));
        }

        private static bool TryParseStoredCommand(string str, out string request, out string response, out SensorCmdType cmdType, out int timeout, out int delay, out int retryCount)
        {
            request = string.Empty;
            response = string.Empty;
            cmdType = SensorCmdType.Ascii;
            timeout = 1000;
            delay = 0;
            retryCount = 0;

            string[] parts = str.Split(',');
            if (parts.Length < 5)
            {
                return false;
            }

            request = parts[0];
            response = string.Join(",", parts.Skip(1).Take(parts.Length - 4));

            if (!Enum.TryParse(parts[^3], out cmdType))
            {
                cmdType = SensorCmdType.Ascii;
            }

            string[] timeParts = parts[^2].Split('/');
            if (timeParts.Length == 2)
            {
                if (!int.TryParse(timeParts[0], out timeout))
                {
                    timeout = 1000;
                }

                if (!int.TryParse(timeParts[1], out delay))
                {
                    delay = 0;
                }
            }

            if (!int.TryParse(parts[^1], out retryCount))
            {
                retryCount = 0;
            }

            return true;
        }
    }



    public class SensorParam : ParamModBase
    {
        [JsonIgnore]
        public SensorTemplatePreset? CreationPreset { get; set; }

        [JsonIgnore]
        public ObservableCollection<SensorCommand> SensorCommands { get; set; }
        public SensorParam()
        {
            ModMaster = new ModMasterModel { Id = -1, Pid = -1, Name = string.Empty, TenantId = 0 };
            Id = -1;
            InitializeCommands();
            CreateCommand = new RelayCommand(_ => ModDetailModels.Add(new ModDetailModel { Pid = -1, ValueA = SensorTemplateDictionaryService.DefaultCommandValue }));
        }
        public SensorParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
            InitializeCommands();
        }

        private void InitializeCommands()
        {
            SensorCommands = new ObservableCollection<SensorCommand>();
            foreach (var mod in ModDetailModels)
            {
                SensorCommands.Add(new SensorCommand(mod));
            }
            // 监听 ModDetailModels 的更改
            ModDetailModels.CollectionChanged += (s, e) =>
            {
                switch (e.Action)
                {
                    // 当有新项被添加时，向 SensorCommands 中添加相应的 SensorCommand
                    case NotifyCollectionChangedAction.Add:
                        if (e.NewItems == null) return;
                        foreach (ModDetailModel newMod in e.NewItems)
                        {
                            SensorCommands.Add(new SensorCommand(newMod));
                        }
                        break;
                    // 当有项被移除时，移除对应的 SensorCommand
                    case NotifyCollectionChangedAction.Remove:
                        if (e.OldItems == null) return;
                        foreach (ModDetailModel oldMod in e.OldItems)
                        {
                            var commandToRemove = SensorCommands.FirstOrDefault(c => c.Model == oldMod);
                            if (commandToRemove != null)
                            {
                                SensorCommands.Remove(commandToRemove);
                            }
                        }
                        break;

                    // 当某项被替换时，更新对应的 SensorCommand
                    case NotifyCollectionChangedAction.Replace:
                        if (e.NewItems == null) return;
                        if (e.OldItems == null) return;
                        for (int i = 0; i < e.NewItems.Count; i++)
                        {
                            var oldMod = (ModDetailModel)e.OldItems[i];
                            var newMod = (ModDetailModel)e.NewItems[i];

                            var commandToReplace = SensorCommands.FirstOrDefault(c => c.Model == oldMod);
                            if (commandToReplace != null && newMod != null)
                            {
                                // 替换为新的 SensorCommand
                                int index = SensorCommands.IndexOf(commandToReplace);
                                SensorCommands[index] = new SensorCommand(newMod);
                            }
                        }
                        break;

                    // 当集合被重置时，清空并重新填充
                    case NotifyCollectionChangedAction.Reset:
                        SensorCommands.Clear();
                        foreach (var mod in ModDetailModels)
                        {
                            SensorCommands.Add(new SensorCommand(mod));
                        }
                        break;
                }
            };
        }

    }
}
