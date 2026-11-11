using LocalizedText = global::ColorVision.Engine.EngineLocalization;
using ColorVision.Engine.Templates.Browser;
#pragma warning disable CA1822,CA1863
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates.Flow.Search;
using ColorVision.Engine.Templates.Flow.Versioning;
using ColorVision.Engine.Templates.Menus;
using ColorVision.UI.Extension;
using ColorVision.UI.Menus;
using log4net;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;

namespace ColorVision.Engine.Templates.Flow
{
    public class MenuTemplateFlow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 0;
        public override string Header => Properties.Resources.MenuFlow;
        public override void Execute()
        {
            new FlowTemplateManagerWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }

    public class TemplateFlow : ITemplate<FlowParam>, IAsyncTemplateLoad
    {
        public override bool UseCatalogCreateSources => true;

        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateFlow));

        public static ObservableCollection<TemplateModel<FlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();

        public static FlowParam? GetParamOrDefault(int index) => Params.ElementAtOrDefault(index)?.Value;

        private readonly LocalFlowTemplateStorage localStorage;
        private readonly Func<bool> isMySqlConnected;
        private readonly Func<SqlSugarClient> openMySql;
        private PreparedFlowPackage? preparedPackage;
        private bool localReadMode;
        private bool UseLocalStorage => localReadMode || !isMySqlConnected();

        public TemplateFlow() : this(LocalFlowTemplateStorage.Default) { }

        public override Window CreateManagerWindow(int selectedIndex = 0) => new FlowTemplateManagerWindow(this, selectedIndex);

        public TemplateFlow(LocalFlowTemplateStorage localStorage, Func<bool>? isMySqlConnected = null, Func<SqlSugarClient>? openMySql = null)
        {
            this.localStorage = localStorage;
            this.isMySqlConnected = isMySqlConnected ?? (() => MySqlSetting.IsConnect);
            this.openMySql = openMySql ?? (() => new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true }));
            IsSideHide = false;
            IsUserControl = true;
            Title = ColorVision.Engine.Properties.Resources.WorkflowEngineTemplateManagement;
            Code = "flow";
            TemplateParams = Params;
        }

        public override void PreviewMouseDoubleClick(int index)
        {
            if (index < 0 || index >= TemplateParams.Count) return;
            new FlowEngineToolWindow(TemplateParams[index].Value) { Owner = Application.Current.GetActiveWindow() }.Show();
        }
        public override bool ExitsTemplateName(string templateName)
        {
            return Params.Any(a => a.Key.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }

        public override void Load() => ApplyValues(ReadValues());

        public async Task LoadAsync()
        {
            // Only detached parameters and storage reads run on the worker. Collection
            // publication, bindings and template registration stay on the UI thread.
            IReadOnlyList<FlowParam> values = await Task.Run(ReadValues).ConfigureAwait(false);
            await Application.Current.Dispatcher.InvokeAsync(() => ApplyValues(values));
        }

        private IReadOnlyList<FlowParam> ReadValues()
        {
            IReadOnlyList<FlowParam> values;
            localReadMode = !isMySqlConnected();
            if (!localReadMode)
            {
                try { values = LoadMySql(); }
                catch (Exception ex)
                {
                    log.Warn("MySQL flow templates unavailable; using local flow templates.", ex);
                    localReadMode = true;
                    values = localStorage.Load();
                }
            }
            else values = localStorage.Load();

            foreach (FlowParam value in values)
                TryAttachCatalogRevision(value);
            return values;
        }

        private void ApplyValues(IReadOnlyList<FlowParam> values)
        {
            var ids = values.Select(value => value.Id).ToHashSet();
            foreach (var removed in TemplateParams.Where(item => !ids.Contains(item.Id)).ToList()) TemplateParams.Remove(removed);
            for (int index = 0; index < values.Count; index++)
            {
                FlowParam value = values[index];
                var existing = TemplateParams.FirstOrDefault(item => item.Id == value.Id);
                if (existing == null) TemplateParams.Insert(index, new TemplateModel<FlowParam>(value.Name, value));
                else
                {
                    existing.Value = value;
                    existing.Key = value.Name;
                    int oldIndex = TemplateParams.IndexOf(existing);
                    // WPF clears ComboBox selection when the selected item is moved to its own index.
                    if (oldIndex != index) TemplateParams.Move(oldIndex, index);
                }
            }
            Title = Properties.Resources.WorkflowEngineTemplateManagement + (localReadMode ? LocalizedText.Get(" · 本地") : " · MySQL");
            SaveIndex.Clear();
        }

        private IReadOnlyList<FlowParam> LoadMySql()
        {
            using var Db = openMySql();
            List<ModMasterModel> flows = Db.Queryable<ModMasterModel>().Where(x => x.Pid == 11).Where(x => x.TenantId == 0).Where(x => x.IsDelete == false).OrderBy(x => x.Id).ToList();
            var values = new List<FlowParam>();
            foreach (var dbModel in flows)
            {
                var details = Db.Queryable<ModDetailModel>().Where(x => x.Pid == dbModel.Id)
                    .Select(it => new ModDetailModel
                    {
                        SysPid = it.SysPid,
                        Pid = it.Pid,
                        ValueA = it.ValueA,
                        ValueB = it.ValueB,
                        IsEnable = it.IsEnable,
                        IsDelete = it.IsDelete,
                        Value = SqlFunc.Subqueryable<SysResourceModel>()
                            .Where(r => r.Id == SqlFunc.ToInt32(it.ValueA))
                            .Select(r => r.Value)
                    })
                    .ToList();
                var param = new FlowParam(dbModel, details);
                AssignRuntimeIdentity(Db, param, details);
                values.Add(param);
            }
            return values;
        }

        public override void Delete(int index)
        {
            foreach (var template in GetManagementTargets(index)) DeleteTemplate(template);
        }

        internal TemplateModel<FlowParam>? FindSavedTemplate(int id, string? flowKey)
            => TemplateParams.FirstOrDefault(template => template.Id == id && string.Equals(template.Value.FlowKey, flowKey, StringComparison.Ordinal));

        internal void DeleteTemplate(TemplateModel<FlowParam> template)
        {
            FlowParam value = template.Value;
            int id = value.Id;
            if (LocalFlowTemplateStorage.IsLocalId(id))
            {
                (value.LocalStorage ?? localStorage).Delete(id);
            }
            else
            {
                if (!isMySqlConnected()) throw new InvalidOperationException("MySQL 未连接，不能删除服务器流程。");
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                List<ModDetailModel> de = Db.Queryable<ModDetailModel>().Where(x => x.Pid == id).ToList();
                Db.Deleteable<ModMasterModel>().Where(x => x.Id == id).ExecuteCommand();

                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                foreach (ModDetailModel model in de)
                {
                    if (int.TryParse(model.ValueA, out int resourceId))
                        Db.Deleteable<SysResourceModel>().Where(x => x.Id == resourceId).ExecuteCommand();
                }
            }
            TemplateParams.Remove(template);
        }

        private TemplateModel<FlowParam>[] GetManagementTargets(int index)
        {
            var selected = TemplateParams.Where(template => template.IsSelected).ToArray();
            return selected.Length == 0 ? [TemplateParams[index]] : selected;
        }

        public override void Save()
        {
            if (SaveIndex.Count == 0) return;

            foreach (var index in SaveIndex)
            {
                if (index > -1 && index < TemplateParams.Count)
                {
                    var item = TemplateParams[index];
                    Save2DB(item.Value);
                }
            }
            SaveIndex.Clear();
        }

        public override void Save(TemplateModel<FlowParam> item) => Save2DB(item.Value);

        public static void Save2DB(
            FlowParam flowParam,
            FlowTemplateSaveCondition? condition = null)
        {
            ArgumentNullException.ThrowIfNull(flowParam);
            if (LocalFlowTemplateStorage.IsLocalId(flowParam.Id))
            {
                (flowParam.LocalStorage ?? LocalFlowTemplateStorage.Default).Save(flowParam, condition);
                TryRecordCatalogRevision(flowParam);
                return;
            }
            if (!MySqlSetting.IsConnect) throw new InvalidOperationException("MySQL 未连接，服务器流程尚未保存。请连接数据库后重试。");
            string? expectedContentHash =
                ResolveExpectedContentHash(flowParam, condition);
            log.Info($"Save2DB: 开始保存, FlowParam.Id={flowParam.Id}, Name={flowParam.Name}, DataBase64长度={flowParam.DataBase64?.Length ?? 0}");
            try
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                SysResourceModel? savedResource = null;
                Db.Ado.BeginTran();
                try
                {
                    flowParam.ModMaster.Name = flowParam.Name;
                    int masterResult = Db.Updateable(flowParam.ModMaster).ExecuteCommand();
                    log.Debug($"Save2DB: 更新ModMaster结果={masterResult}");

                    List<ModDetailModel> details = new();
                    flowParam.GetDetail(details);
                    log.Debug($"Save2DB: details数量={details.Count}");
                    if (details.Count > 0)
                    {
                        var model = details[0];
                        SysResourceModel? res = null;
                        int id = 0;
                        bool hasId = int.TryParse(model.ValueA, out id);

                        log.Debug($"Save2DB: model.ValueA={model.ValueA}, hasId={hasId}, id={id}");
                        if (hasId)
                        {
                            // Lock the compatibility resource until commit so
                            // two editors cannot both pass the hash check and
                            // silently overwrite one another.
                            Db.Ado.SqlQuery<int>(
                                """
                                SELECT id
                                FROM t_scgd_sys_resource
                                WHERE id = @id
                                FOR UPDATE
                                """,
                                new { id });
                            res = Db.Queryable<SysResourceModel>().InSingle(id);
                        }

                        if (res != null)
                        {
                            string actualHash = ComputeCanvasHash(
                                res.Value ?? string.Empty);
                            if (!string.IsNullOrWhiteSpace(
                                    expectedContentHash)
                                && !string.Equals(
                                    expectedContentHash,
                                    actualHash,
                                    StringComparison.Ordinal))
                            {
                                throw new FlowTemplateConcurrencyException(
                                    flowParam.Name,
                                    expectedContentHash,
                                    actualHash);
                            }

                            // 资源已存在，更新
                            res.Name = flowParam.Name;
                            res.Value = flowParam.DataBase64;
                            if (string.IsNullOrWhiteSpace(res.Code))
                                res.Code = Guid.NewGuid().ToString("N");
                            int updateResult = Db.Updateable(res).ExecuteCommand();
                            model.ValueA = res.Id.ToString();
                            log.Info($"Save2DB: 更新资源成功, ResId={res.Id}, updateResult={updateResult}");
                        }
                        else
                        {
                            // 新建资源
                            res = new SysResourceModel
                            {
                                Name = flowParam.Name,
                                Type = 101,
                                Value = flowParam.DataBase64,
                                Code = Guid.NewGuid().ToString("N")
                            };
                            res.Id = Db.Insertable(res).ExecuteReturnIdentity();
                            model.ValueA = res.Id.ToString();
                            log.Info($"Save2DB: 新建资源成功, ResId={res.Id}");
                        }

                        // 3. 更新明细表
                        int detailResult = Db.Updateable(details)
                            .Where(md => md.Pid == flowParam.Id)
                            .ExecuteCommand();
                        log.Debug($"Save2DB: 更新明细表结果={detailResult}");
                        savedResource = res;
                    }
                    else
                    {
                        log.Warn("Save2DB: details为空, 没有明细数据需要保存");
                    }

                    Db.Ado.CommitTran();
                }
                catch
                {
                    try
                    {
                        Db.Ado.RollbackTran();
                    }
                    catch (Exception rollbackException)
                    {
                        log.Error("Save2DB: 回滚数据库事务失败", rollbackException);
                    }
                    throw;
                }

                if (savedResource != null)
                    AssignRuntimeIdentity(flowParam, savedResource);
                UpdateLoadedContentHash(flowParam);
                TryRecordCatalogRevision(flowParam);
                log.Info("Save2DB: 保存完成");
            }
            catch (Exception ex)
            {
                log.Error("Save2DB: 保存流程到数据库时发生异常", ex);
                throw;
            }
        }


        internal static void ExportTemplate(TemplateModel<FlowParam> template)
        {
            string name = template.Key;
            string dataBase64 = template.Value.DataBase64;
            using System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            bool isLocal = LocalFlowTemplateStorage.IsLocalId(template.Id);
            sfd.DefaultExt = isLocal ? "stn" : "cvflow";
            sfd.Filter = isLocal ? "STN files (*.stn)|*.stn" : ColorVision.Engine.Properties.Resources.Flow_ExportFlowFilter;
            sfd.AddExtension = true;
            sfd.RestoreDirectory = true;
            sfd.Title = ColorVision.Engine.Properties.Resources.Flow_ExportFlow;
            sfd.FileName = Tool.SanitizeFileName(name);
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            byte[] fileBytes = Convert.FromBase64String(dataBase64);
            if (sfd.FileName.EndsWith(".cvflow", StringComparison.OrdinalIgnoreCase))
            {
                var manifest = FlowPackageHelper.CollectTemplatesForExport(name, fileBytes);
                FlowPackageHelper.ExportFlowPackage(sfd.FileName, name, fileBytes, manifest);
            }
            else
            {
                File.WriteAllBytes(sfd.FileName, fileBytes);
            }
        }

        public override void Export(int index)
        {
            var targets = GetManagementTargets(index);
            if (targets.Length == 1)
            {
                ExportTemplate(targets[0]);
                return;
            }

            using System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
            sfd.DefaultExt = "zip";
            sfd.Filter = "*.zip|*.zip";
            sfd.AddExtension = true;
            sfd.RestoreDirectory = true;
            sfd.Title = ColorVision.Engine.Properties.Resources.Export;
            sfd.FileName = $"{Code}.zip";
            if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

            string tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(tempDirectory);
            try
            {
                foreach (var template in targets)
                {
                    string filePath = Path.Combine(tempDirectory, $"{Tool.SanitizeFileName(template.Key)}.stn");
                    byte[] fileBytes = Convert.FromBase64String(template.Value.DataBase64);
                    File.WriteAllBytes(filePath, fileBytes);
                }

                using var zipToOpen = new FileStream(sfd.FileName, FileMode.Create);
                using var archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create);
                foreach (string filePath in Directory.GetFiles(tempDirectory))
                    archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
            }
            finally
            {
                Directory.Delete(tempDirectory, true);
            }
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = UseLocalStorage ? "STN files (*.stn)|*.stn" : ColorVision.Engine.Properties.Resources.Flow_ImportFlowFilter;
            ofd.Title = ColorVision.Engine.Properties.Resources.ImportFlow;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            return ImportFile(ofd.FileName);
        }

        public override bool ImportFile(string filePath)
        {
            ClearCreateTemplateSource();
            if (!File.Exists(filePath)) return false;
            if (UseLocalStorage && filePath.EndsWith(".stn", StringComparison.OrdinalIgnoreCase))
            {
                byte[] bytes = File.ReadAllBytes(filePath);
                FlowPackageStnValidator.ValidateAndDecompress(bytes);
                ImportName = Path.GetFileNameWithoutExtension(filePath);
                ImportTemp = new FlowParam { Id = -1, DataBase64 = Convert.ToBase64String(bytes) };
                return true;
            }
            if (UseLocalStorage) throw new NotSupportedException("本地流程目前支持导入 .stn 画布，关联模板包仍需在服务模式下导入。");

            return filePath.EndsWith(".cvflow", StringComparison.OrdinalIgnoreCase) && PrepareFlowPackage(filePath);
        }

        public override bool ImportJsonContent(string templateName, string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return false;

            try
            {
                preparedPackage = null;
                ImportName = templateName;
                ImportTemp = JsonConvert.DeserializeObject<FlowParam>(jsonContent);
                if (ImportTemp != null)
                {
                    ImportTemp.Id = -1;
                    return true;
                }
            }
            catch (JsonException ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Flow_ParseFlowSampleError, ex.Message), "ColorVision");
            }

            return false;
        }

        private bool PrepareFlowPackage(string filePath)
        {
            try
            {
                var (stnData, manifest) = FlowPackageHelper.ImportFlowPackage(filePath);
                if (stnData == null || stnData.Length == 0)
                    return false;

                string flowName = Path.GetFileNameWithoutExtension(filePath);
                if (manifest != null && !string.IsNullOrEmpty(manifest.FlowName))
                    flowName = manifest.FlowName;

                ImportName = flowName;
                var draft = new FlowParam { Id = -1, DataBase64 = Convert.ToBase64String(stnData) };
                ImportTemp = draft;
                preparedPackage = new PreparedFlowPackage(draft, flowName, manifest);
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Flow_ImportFlowPackageError, ex.Message), "ColorVision");
                return false;
            }
        }

        public override void ClearCreateTemplateSource()
        {
            preparedPackage = null;
            base.ClearCreateTemplateSource();
        }

        internal static string? ResolveExpectedContentHash(
            FlowParam flowParam,
            FlowTemplateSaveCondition? condition)
        {
            ArgumentNullException.ThrowIfNull(flowParam);
            return condition == null
                ? flowParam.LoadedContentHash
                : condition.ExpectedContentHash;
        }



        public override bool CopyTo(int index)
        {
            if (index > -1 && index < TemplateParams.Count)
            {
                preparedPackage = null;
                if (LocalFlowTemplateStorage.IsLocalId(TemplateParams[index].Id))
                {
                    ImportTemp = new FlowParam { Id = -1, DataBase64 = TemplateParams[index].Value.DataBase64 };
                    return true;
                }
                string fileContent = TemplateParams[index].Value.ToJsonN();
                ImportTemp = JsonConvert.DeserializeObject<FlowParam>(fileContent);
                if (ImportTemp != null)
                {
                    ImportTemp.Id = -1;
                }
                return true;
            }
            return false;
        }

        public override void Create(string templateName)
        {
            PreparedFlowPackage? package = preparedPackage != null && ReferenceEquals(preparedPackage.Draft, ImportTemp)
                ? preparedPackage : null;
            if (UseLocalStorage)
            {
                if (package != null) throw new NotSupportedException("关联模板包仍需在服务模式下导入。");
                var local = new FlowParam { Id = -1, Name = templateName, DataBase64 = ImportTemp?.DataBase64 ?? string.Empty };
                localStorage.Save(local);
                TemplateParams.Add(new TemplateModel<FlowParam>(templateName, local));
                ImportTemp = null;
                TryRecordCatalogRevision(local);
                return;
            }
            string? importData = ImportTemp?.DataBase64;
            if (package != null)
            {
                ValidatePackageDestination(templateName);
                if (package.Manifest is { Templates.Count: > 0 } manifest)
                {
                    var nameMap = FlowPackageHelper.ImportTemplates(manifest, package.FlowName);
                    if (nameMap.Count > 0)
                        importData = Convert.ToBase64String(FlowPackageHelper.ReplaceTemplateNames(Convert.FromBase64String(package.Draft.DataBase64), nameMap));
                }
            }
            FlowParam? param = AddServerFlowParam(templateName);
            if (param != null)
            {
                if (importData != null)
                {
                    param.DataBase64 = importData;
                    Save2DB(param);
                    ImportTemp = null;
                    preparedPackage = null;
                }
                var a = new TemplateModel<FlowParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.CreateDataBase+$"{typeof(FlowParam)}"+ ColorVision.Engine.Properties.Resources.TemplateFailed, "ColorVision");
            }
        }

        private void ValidatePackageDestination(string templateName)
        {
            if (string.IsNullOrWhiteSpace(templateName))
                throw new ArgumentException(Properties.Resources.InputTemplateName, nameof(templateName));
            if (!ExitsTemplateName(templateName))
            {
                using var db = openMySql();
                if (!db.Queryable<ModMasterModel>().Any(item => item.Pid == 11 && item.TenantId == 0 && !item.IsDelete && item.Name == templateName))
                    return;
            }
            throw new InvalidOperationException(LocalizedText.Format($"流程模板 {templateName} 已存在。"));
        }

        private sealed record PreparedFlowPackage(FlowParam Draft, string FlowName, FlowPackageManifest? Manifest);

        public FlowParam? AddFlowParam(string templateName)
        {
            if (UseLocalStorage)
            {
                var local = new FlowParam { Id = -1, Name = templateName };
                localStorage.Save(local);
                return local;
            }
            return AddServerFlowParam(templateName);
        }

        private FlowParam? AddServerFlowParam(string templateName)
        {
            using var Db = openMySql();
            FlowParam? created = null;
            Db.Ado.BeginTran();
            try
            {
                var flowMaster = new ModMasterModel()
                {
                    Pid = 11,
                    Name = templateName,
                    TenantId = 0
                };
                flowMaster.Id =
                    Db.Insertable(flowMaster).ExecuteReturnIdentity();
                if (flowMaster.Id <= 0)
                    throw new InvalidOperationException("创建流程主记录失败。");

                List<ModDetailModel> list = new();
                foreach (var item in
                    SysDictionaryModDetailDao.Instance.GetAllByPid(
                        flowMaster.Pid))
                {
                    list.Add(new ModDetailModel
                    {
                        SysPid = item.Id,
                        Pid = flowMaster.Id,
                        ValueA = item.DefaultValue
                    });
                }
                Db.Insertable(list).ExecuteCommand();

                var flowDetail = Db.Queryable<ModDetailModel>()
                    .Where(item => item.Pid == flowMaster.Id)
                    .ToList();
                if (flowDetail.Count > 0
                    && int.TryParse(
                        flowDetail[0].ValueA,
                        out int defaultResourceId))
                {
                    var defaultResource =
                        Db.Queryable<SysResourceModel>()
                            .InSingle(defaultResourceId);
                    if (defaultResource != null)
                    {
                        flowDetail[0].Value = defaultResource.Value;
                        var resource = new SysResourceModel
                        {
                            Name = flowMaster.Name,
                            Code = Guid.NewGuid().ToString("N"),
                            Type = defaultResource.Type,
                            Value = defaultResource.Value
                        };
                        resource.Id = Db.Insertable(resource)
                            .ExecuteReturnIdentity();
                        flowDetail[0].ValueA =
                            resource.Id.ToString();
                        Db.Updateable(flowDetail[0])
                            .ExecuteCommand();
                    }
                }

                created = new FlowParam(flowMaster, flowDetail);
                AssignRuntimeIdentity(Db, created, flowDetail);
                Db.Ado.CommitTran();
            }
            catch
            {
                try
                {
                    Db.Ado.RollbackTran();
                }
                catch (Exception rollbackException)
                {
                    log.Error(
                        "AddFlowParam: 回滚数据库事务失败",
                        rollbackException);
                }
                throw;
            }

            TryRecordCatalogRevision(created);
            return created;
        }

        public override IEnumerable<TemplateCreatePreset> GetCreatePresets() => FlowTemplatePresets.All.Select(preset => preset.Source);

        public override bool ApplyCreatePreset(string presetId)
        {
            var preset = FlowTemplatePresets.Find(presetId);
            if (preset == null) return false;
            preparedPackage = null;
            ImportTemp = FlowTemplatePresets.CreateDraft(preset);
            CreateTemp = null;
            ImportName = preset.Source.SuggestedName;
            return true;
        }

        public override object CreateDefault() => CreateTemp = new FlowParam
        {
            Id = -1, Name = ImportTemp?.Name ?? string.Empty, DataBase64 = ImportTemp?.DataBase64 ?? string.Empty,
            CreationPreset = ImportTemp?.CreationPreset
        };

        public override UserControl CreateUserControl() => new FlowTemplateCreatePreview();

        private static void TryRecordCatalogRevision(FlowParam flowParam)
        {
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey)
                || string.IsNullOrWhiteSpace(flowParam.DataBase64))
            {
                flowParam.TemplateRevision = null;
                flowParam.TemplateContentHash = null;
                return;
            }

            try
            {
                byte[] canvasData =
                    Convert.FromBase64String(flowParam.DataBase64);
                flowParam.LoadedContentHash =
                    FlowSemanticHash.ComputeBinaryHash(canvasData);
                FlowCatalogService catalog =
                    FlowCatalogProvider.Shared;
                FlowCanvasCatalogBuildResult projection =
                    new FlowCanvasCatalogBuilder().Build(canvasData);
                FlowNodeSearchDocument[] searchDocuments =
                    projection.SearchDocuments
                        .Select(document =>
                            WithFlowTemplateName(
                                document,
                                flowParam.Name))
                        .ToArray();
                FlowRevision revision = catalog.RecordEditorSave(
                    flowParam.FlowKey,
                    canvasData,
                    projection.SemanticDocument,
                    searchDocuments,
                    message: $"Save template {flowParam.Name}");
                flowParam.TemplateRevision = revision.Revision;
                flowParam.TemplateContentHash = revision.BinaryHash;
            }
            catch (Exception ex)
            {
                // The template/STN save is the persistence contract. A local
                // catalog failure must not turn a valid save into a false
                // failure.
                flowParam.TemplateRevision = null;
                flowParam.TemplateContentHash = null;
                log.Warn(
                    $"流程 {flowParam.Name} 已保存，仅版本历史/节点搜索索引未更新，不影响流程保存和执行。",
                    ex);
            }
        }

        internal static void RefreshCatalogProjection(
            FlowParam flowParam)
        {
            ArgumentNullException.ThrowIfNull(flowParam);
            TryRecordCatalogRevision(flowParam);
        }

        private static void UpdateLoadedContentHash(
            FlowParam flowParam)
        {
            try
            {
                flowParam.LoadedContentHash =
                    ComputeCanvasHash(
                        flowParam.DataBase64
                            ?? string.Empty);
            }
            catch (FormatException)
            {
                flowParam.LoadedContentHash = null;
            }
        }

        private static string ComputeCanvasHash(
            string dataBase64)
        {
            return FlowSemanticHash.ComputeBinaryHash(
                Convert.FromBase64String(
                    dataBase64 ?? string.Empty));
        }

        private static void TryAttachCatalogRevision(FlowParam flowParam)
        {
            flowParam.TemplateRevision = null;
            flowParam.TemplateContentHash = null;
            UpdateLoadedContentHash(flowParam);
            if (string.IsNullOrWhiteSpace(flowParam.FlowKey)
                || string.IsNullOrWhiteSpace(flowParam.DataBase64))
            {
                return;
            }

            try
            {
                FlowRevision? revision =
                    FlowCatalogProvider.Shared.FindRevision(
                        flowParam.FlowKey,
                        Convert.FromBase64String(
                            flowParam.DataBase64));
                if (revision == null)
                    return;

                flowParam.TemplateRevision = revision.Revision;
                flowParam.TemplateContentHash = revision.BinaryHash;
            }
            catch (Exception ex)
            {
                log.Error(
                    $"读取流程 {flowParam.Name} 的版本目录失败。",
                    ex);
            }
        }

        private static FlowNodeSearchDocument WithFlowTemplateName(
            FlowNodeSearchDocument source,
            string? flowName)
        {
            return new FlowNodeSearchDocument
            {
                SourceNodeGuid = source.SourceNodeGuid,
                NodePath = source.NodePath,
                NodeTypeKey = source.NodeTypeKey,
                DisplayName = source.DisplayName,
                Title = source.Title,
                TemplateName = string.IsNullOrWhiteSpace(
                        source.TemplateName)
                    ? flowName
                    : source.TemplateName,
                DeviceCode = source.DeviceCode,
                ServiceCode = source.ServiceCode,
                Tags = source.Tags,
            };
        }

        private static void AssignRuntimeIdentity(
            SqlSugarClient db,
            FlowParam flowParam,
            List<ModDetailModel> details)
        {
            if (details.Count == 0
                || !int.TryParse(details[0].ValueA, out int resourceId)
                || resourceId <= 0)
            {
                flowParam.FlowKey = FlowTemplateIdentity.Create(
                    flowParam.Id,
                    null,
                    null);
                return;
            }

            SysResourceModel? resource = db.Queryable<SysResourceModel>()
                .Where(item => item.Id == resourceId)
                .Select(item => new SysResourceModel
                {
                    Id = item.Id,
                    Code = item.Code
                })
                .First();
            if (resource != null)
            {
                AssignRuntimeIdentity(flowParam, resource);
            }
            else
            {
                flowParam.ResourceId = resourceId;
                flowParam.FlowKey = FlowTemplateIdentity.Create(
                    flowParam.Id,
                    resourceId,
                    null);
            }
        }

        private static void AssignRuntimeIdentity(
            FlowParam flowParam,
            SysResourceModel resource)
        {
            flowParam.ResourceId = resource.Id;
            flowParam.ResourceCode = resource.Code;
            flowParam.FlowKey = FlowTemplateIdentity.Create(
                flowParam.Id,
                resource.Id,
                resource.Code);
        }
    }
}
