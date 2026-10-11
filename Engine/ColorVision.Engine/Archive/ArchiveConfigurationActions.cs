using LocalizedText = global::ColorVision.Engine.EngineLocalization;
#pragma warning disable CA1863
using ColorVision.Database;
using ColorVision.Engine.Archive.Dao;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using log4net;
using System.Windows;

namespace ColorVision.Engine.Archive
{
    internal static class ArchiveConfigurationActions
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ArchiveConfigurationActions));

        internal static void OpenServerConfiguration()
        {
            GlobleCfgdModel? globleCfgdModel = GlobleCfgdDao.Instance.GetArchDB();
            if (globleCfgdModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ArchiveServerConfigNotFound_Resetting, "ColorVision");
                string sql = "INSERT INTO `cv`.`t_scgd_sys_globle_cfg` (`id`, `code`, `name`, `cfg_type`, `cfg_value`, `is_deleted`, `is_enabled`, `remark`, `tenant_id`) VALUES (3, 'arch_db', '归档服务数据库', 10, '{\\\"Name\\\":null,\\\"Host\\\":\\\"localhost\\\",\\\"Port\\\":3306,\\\"UserName\\\":\\\"cv\\\",\\\"UserPwd\\\":\\\"9p9DMdywXwaTbAXt0oJkUnAb\\\",\\\"Database\\\":\\\"color_vision_arch_2025\\\"}', 0, 1, NULL, 0);\r\n";
                try
                {
                    globleCfgdModel = BatchSqlConsumer.ExecuteAfterCommit(sql, GlobleCfgdDao.Instance.GetArchDB);
                }
                catch (BatchExecuteNonQueryException ex)
                {
                    ShowBatchFailure("初始化归档服务数据库配置", ex);
                    return;
                }
            }
            if (globleCfgdModel == null)
                return;

            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(globleCfgdModel, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submitted += (s, e) => { GlobleCfgdDao.Instance.Save(globleCfgdModel); };
            propertyEditorWindow.ShowDialog();
        }

        internal static void OpenArchiveConfiguration()
        {
            const string operation = "更新归档配置数据库结构";
            try
            {
                ArchiveConfigurationSchemaMigration.EnsureColumnsAndExecute(OpenArchiveConfigurationEditor);
            }
            catch (BatchExecuteNonQueryException ex)
            {
                ShowBatchFailure(operation, ex);
            }
            catch (ArchiveSchemaMigrationException ex)
            {
                ShowSchemaInspectionFailure(operation, ex);
            }
        }

        private static void OpenArchiveConfigurationEditor()
        {
            SysConfigRcModel? sysConfigRcModel = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (sysConfigRcModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.RcConfigInfoNotFound, "ColorVision");
                return;
            }
            ConfigArchivedModel? configArchivedModel = ConfigArchivedDao.Instance.GetById(sysConfigRcModel.ArchivedId);
            if (configArchivedModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.ArchiveConfigInfoNotFound, "ColorVision");
                return;
            }

            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(configArchivedModel, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submitted += (s, e) => { ConfigArchivedDao.Instance.Save(configArchivedModel); };
            propertyEditorWindow.ShowDialog();
        }

        private static void ShowBatchFailure(string operation, BatchExecuteNonQueryException exception)
        {
            BatchSqlConsumer.ReportUiFailure(log, operation, exception);
        }

        private static void ShowSchemaInspectionFailure(string operation, ArchiveSchemaMigrationException exception)
        {
            log.Error($"{operation}失败。{exception.GetDiagnosticSummary()}");
            MessageBox.Show(
                Application.Current.GetActiveWindow(),
                LocalizedText.Format($"{operation}失败，后续操作已停止。\r\n错误标识：{exception.Stage} / {exception.FailureType} ({exception.ErrorCode})。\r\n请检查日志或联系管理员。"),
                "ColorVision");
        }

        internal static void OpenServiceRegistryConfiguration()
        {
            SysConfigRcModel? sysConfigRcModel = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (sysConfigRcModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_RCConfigInfoNotFound, RCSetting.Instance.Config.RCName), "ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(sysConfigRcModel, PropertyEditorEditMode.Transactional) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submitted += (s, e) => { SysConfigRcDao.Instance.Save(sysConfigRcModel); };
            propertyEditorWindow.ShowDialog();
        }

    }
}
