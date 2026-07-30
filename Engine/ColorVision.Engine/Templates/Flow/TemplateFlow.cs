#pragma warning disable CA1822,CA1863
using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Compilation;
using ColorVision.Engine.FlowProcessing.Editor;
using ColorVision.Engine.Templates.Flow.Routing;
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

namespace ColorVision.Engine.Templates.Flow
{
    public class MenuTemplateFlow : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuTemplate);
        public override int Order => 0;
        public override string Header => Properties.Resources.MenuFlow;
        public override void Execute()
        {
            new TemplateEditorWindow(new TemplateFlow()) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog(); ;
        }
    }

    public class TemplateFlow : ITemplate<FlowParam>, IITemplateLoad
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateFlow));

        public static ObservableCollection<TemplateModel<FlowParam>> Params { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();


        public TemplateFlow()
        {
            IsSideHide = true;
            Title = ColorVision.Engine.Properties.Resources.WorkflowEngineTemplateManagement;
            Code = "flow";
            TemplateParams = Params;
        }

        public override void PreviewMouseDoubleClick(int index)
        {
            new FlowEngineToolWindow(TemplateParams[index].Value) { Owner = Application.Current.GetActiveWindow() }.Show();
        }
        public override bool ExitsTemplateName(string templateName)
        {
            return Params.Any(a => a.Key.Equals(templateName, StringComparison.OrdinalIgnoreCase));
        }

        public override void Load()
        {
            
            var backup = TemplateParams.ToDictionary(tp => tp.Id, tp => tp);
            if (MySqlSetting.IsConnect)
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                List<ModMasterModel> flows = Db.Queryable<ModMasterModel>().Where(x => x.Pid == 11).Where(x => x.TenantId == 0).Where(x => x.IsDelete == false).ToList();
                foreach (var dbModel in flows)
                {
                    var details = Db.Queryable<ModDetailModel>().Where(x=>x.Pid == dbModel.Id)
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
                    TryAttachCatalogRevision(param);

                    if (backup.TryGetValue(param.Id, out var model))
                    {
                        model.Value = param;
                        model.Key = param.Name;

                    }
                    else
                    {
                        var item = new TemplateModel<FlowParam>(dbModel.Name ?? "default", param);
                        TemplateParams.Add(item);
                    }
                }
            }
            SaveIndex.Clear();
        }

        public override void Delete(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            void DeleteSingle(int id)
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                List<ModDetailModel> de = Db.Queryable<ModDetailModel>().Where(x => x.Pid == id).ToList();
                int ret = Db.Deleteable<ModMasterModel>().Where(x => x.Id == id).ExecuteCommand();

                Db.Deleteable<ModDetailModel>().Where(x => x.Pid == id).ExecuteCommand();
                foreach (ModDetailModel model in de)
                {
                    if (int.TryParse(model.ValueA, out int resourceId))
                        ret = Db.Deleteable<SysResourceModel>().Where(x => x.Id == resourceId).ExecuteCommand();
                }
            }

            if (selectedCount <= 1)
            {
                int id = TemplateParams[index].Value.Id;
                DeleteSingle(id);
                TemplateParams.RemoveAt(index);
            }
            else
            {
                foreach (var item in TemplateParams.Where(item => item.IsSelected == true).ToList())
                {
                    DeleteSingle(item.Id);
                    TemplateParams.Remove(item);
                }
            }
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
        }

        public static void Save2DB(
            FlowParam flowParam,
            FlowTemplateSaveCondition? condition = null)
        {
            ArgumentNullException.ThrowIfNull(flowParam);
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


        public override void Export(int index)
        {
            int selectedCount = TemplateParams.Count(item => item.IsSelected);
            if (selectedCount == 1) index = TemplateParams.IndexOf(TemplateParams.First(item => item.IsSelected));

            if (selectedCount <= 1)
            {
                using System.Windows.Forms.SaveFileDialog sfd = new System.Windows.Forms.SaveFileDialog();
                sfd.DefaultExt = "cvflow";
                sfd.Filter = ColorVision.Engine.Properties.Resources.Flow_ExportFlowFilter;
                sfd.AddExtension = true;
                sfd.RestoreDirectory = true;
                sfd.Title = ColorVision.Engine.Properties.Resources.Flow_ExportFlow;
                sfd.FileName = Tool.SanitizeFileName(TemplateParams[index].Key);
                if (sfd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                byte[] fileBytes = Convert.FromBase64String(TemplateParams[index].Value.DataBase64);

                if (sfd.FileName.EndsWith(".cvflow", StringComparison.OrdinalIgnoreCase))
                {
                    // 导出流程包 (STN + 关联模板)
                    var manifest = FlowPackageHelper.CollectTemplatesForExport(TemplateParams[index].Key, fileBytes);
                    FlowPackageHelper.ExportFlowPackage(sfd.FileName, TemplateParams[index].Key, fileBytes, manifest);
                }
                else
                {
                    File.WriteAllBytes(sfd.FileName, fileBytes);
                }
            }
            else
            {
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
                    // 导出所有模板文件到临时目录
                    foreach (var kvp in TemplateParams.Where(item => item.IsSelected == true))
                    {
                        string filePath = Path.Combine(tempDirectory, $"{Tool.SanitizeFileName(kvp.Key)}.stn");
                        byte[] fileBytes = Convert.FromBase64String(kvp.Value.DataBase64);
                        File.WriteAllBytes(filePath, fileBytes);
                    }

                    // 创建压缩文件
                    using (FileStream zipToOpen = new FileStream(sfd.FileName, FileMode.Create))
                    {
                        using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Create))
                        {
                            foreach (string filePath in Directory.GetFiles(tempDirectory))
                            {
                                archive.CreateEntryFromFile(filePath, Path.GetFileName(filePath));
                            }
                        }
                    }
                }
                finally
                {
                    // 清理临时目录
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        public override bool Import()
        {
            System.Windows.Forms.OpenFileDialog ofd = new System.Windows.Forms.OpenFileDialog();
            ofd.Filter = ColorVision.Engine.Properties.Resources.Flow_ImportFlowFilter;
            ofd.Title = ColorVision.Engine.Properties.Resources.ImportFlow;
            ofd.RestoreDirectory = true;
            if (ofd.ShowDialog() != System.Windows.Forms.DialogResult.OK) return false;
            return ImportFile(ofd.FileName);
        }

        public override bool ImportFile(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                if (filePath.EndsWith(".cvflow", StringComparison.OrdinalIgnoreCase))
                {
                    return ImportFlowPackage(filePath);
                }

            }
            catch (Exception ex)
            {
                log.Error(ex);

                ImportName = Path.GetFileNameWithoutExtension(filePath);
                byte[] fileBytes = File.ReadAllBytes(filePath);
                string base64 = Convert.ToBase64String(fileBytes);
                FlowParam param = new FlowParam();
                param.DataBase64 = base64;
                ImportTemp = param;
            }

            return true;
        }

        public override bool ImportJsonContent(string templateName, string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(jsonContent)) return false;

            try
            {
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

        /// <summary>
        /// 导入 .cvflow 流程包文件，自动创建关联模板并更新流程中的模板引用
        /// </summary>
        private bool ImportFlowPackage(string filePath)
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

                // 导入关联模板，获取名称映射 (旧名称 → 新名称)
                Dictionary<string, string> nameMap = new Dictionary<string, string>();
                if (manifest?.Templates != null && manifest.Templates.Count > 0)
                {
                    nameMap = FlowPackageHelper.ImportTemplates(manifest, flowName);
                }

                // 如果有模板名称发生了变更，更新 STN 中的引用
                byte[] finalStnData = stnData;
                if (nameMap.Count > 0)
                {
                    finalStnData = FlowPackageHelper.ReplaceTemplateNames(stnData, nameMap);
                }

                string base64 = Convert.ToBase64String(finalStnData);
                FlowParam param = new FlowParam();
                param.DataBase64 = base64;
                ImportTemp = param;
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Flow_ImportFlowPackageError, ex.Message), "ColorVision");
                return false;
            }
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
            FlowParam? param = AddFlowParam(templateName);
            if (param != null)
            {
                if (ImportTemp != null)
                {
                    param.DataBase64 = ImportTemp.DataBase64;
                    Save2DB(param);
                    ImportTemp = null;
                }
                var a = new TemplateModel<FlowParam>(templateName, param);
                TemplateParams.Add(a);
            }
            else
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.CreateDataBase+$"{typeof(FlowParam)}"+ ColorVision.Engine.Properties.Resources.TemplateFailed, "ColorVision");
            }
        }
        public FlowParam? AddFlowParam(string templateName)
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
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
                FlowRevision? requestedRevision =
                    flowParam.TemplateRevision is int revisionNumber
                        && revisionNumber > 0
                        ? catalog.GetRevision(
                            flowParam.FlowKey,
                            revisionNumber)
                        : null;
                if (requestedRevision != null
                    && !string.Equals(
                        requestedRevision.BinaryHash,
                        flowParam.LoadedContentHash,
                        StringComparison.Ordinal))
                {
                    requestedRevision = null;
                }
                FlowRevision? matching =
                    catalog.FindRevision(flowParam.FlowKey, canvasData);
                FlowRevision? inheritanceRevision =
                    requestedRevision
                    ?? matching
                    ?? catalog.GetHead(flowParam.FlowKey);
                FlowSubflowSidecar subflows =
                    inheritanceRevision == null
                        ? FlowSubflowSidecar.Empty
                        : FlowSubflowDefinitionStoreProvider.Shared
                            .GetRevision(
                                flowParam.FlowKey,
                                inheritanceRevision.Revision)
                            ?.Sidecar
                            ?? FlowSubflowSidecar.Empty;

                if (!FlowExecutionPolicyStoreProvider.Shared.TryLoad(
                        flowParam.FlowKey,
                        out FlowExecutionPolicySnapshot executionPolicy,
                        out string? policyFailure))
                {
                    log.Error(
                        $"流程 {flowParam.Name} 的执行策略无法读取，"
                        + $"跳过版本目录更新：{policyFailure}");
                    return;
                }

                FlowCanvasCatalogBuildResult projection =
                    new FlowCanvasCatalogBuilder().Build(
                        canvasData,
                        subflows,
                        executionPolicy);
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
                FlowSubflowDefinitionStoreProvider.Shared.Append(
                    flowParam.FlowKey,
                    revision.Revision,
                    subflows);
                flowParam.TemplateRevision = revision.Revision;
                flowParam.TemplateContentHash = revision.BinaryHash;
            }
            catch (Exception ex)
            {
                // The MySQL/STN save is the compatibility contract. A local
                // sidecar failure must be visible, but must not turn a valid
                // legacy save into a false failure.
                flowParam.TemplateRevision = null;
                flowParam.TemplateContentHash = null;
                log.Error(
                    $"流程 {flowParam.Name} 已保存，但版本/搜索侧车更新失败。",
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
