using ColorVision.Engine.Archive.Dao;
using ColorVision.Database;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.Menus;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Sys.Dao
{
    [SugarTable("t_scgd_sys_config_rc")]
    public class SysConfigRcModel : ViewEntity 
    {
        [DisplayName("Code")]
        public string Code { get; set; }

        [SugarColumn(ColumnName ="name"), DisplayName("RCName")]
        public string Name { get; set; }

        [SugarColumn(ColumnName ="platform")]
        public string Platform { get; set; }

        [SugarColumn(ColumnName ="rest_address"), DisplayName("RESTAddress")]
        public string RestAddress { get; set; }

        [SugarColumn(ColumnName ="version")]
        public string Version { get; set; }

        [SugarColumn(ColumnName ="token_expires")]
        public int? TokenExpires { get; set; }

        [SugarColumn(ColumnName ="admin_authorization")]
        public string AdminAuthorization { get; set; }

        [SugarColumn(ColumnName ="monitor_id"), DisplayName("ServerMonitorConfig")]
        public int? MonitorId { get; set; }

        [SugarColumn(ColumnName ="archived_id"), DisplayName("ArchiveConfiguration")]
        public int? ArchivedId { get; set; }

        [SugarColumn(ColumnName ="mqtt_cfg_id"), DisplayName("MqttConfigId")]
        public int? MqttCfgId { get; set; }

        [SugarColumn(ColumnName ="mqtt_is_server")]
        public bool MqttIsServer { get; set; }

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; }

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName ="tenant_id"), DisplayName("TenantId")]
        public int? TenantId { get; set; }

        [SugarColumn(ColumnName ="create_date"), DisplayName("CreationDate")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get; set; }
    }


    public class SysConfigRcDao : BaseTableDao<SysConfigRcModel>
    {
        public static SysConfigRcDao Instance { get; set; } = new SysConfigRcDao();

        public SysConfigRcModel? GetByCode(string code)
        {
            return this.GetByParam(new Dictionary<string, object>() { { "code", code } });
        }
    }
    public class MenuConfigArchive : MenuItemBase
    {
        public override string OwnerGuid => nameof(MenuArchive);

        public override string Header => "服务注册中心配置";

        public override void Execute()
        {
            SysConfigRcModel sysConfigRcModel = SysConfigRcDao.Instance.GetByCode(RCSetting.Instance.Config.RCName);
            if (sysConfigRcModel == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"找不到{RCSetting.Instance.Config.RCName}配置信息", "ColorVision");
                return;
            }
            PropertyEditorWindow propertyEditorWindow = new PropertyEditorWindow(sysConfigRcModel, false) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner };
            propertyEditorWindow.Submited += (s, e) => { SysConfigRcDao.Instance.Save(sysConfigRcModel); };
            propertyEditorWindow.ShowDialog();
        }
    }
}
