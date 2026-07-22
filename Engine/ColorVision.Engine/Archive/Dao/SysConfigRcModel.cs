using ColorVision.Database;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using ColorVision.Engine.Utilities;
using ColorVision.Engine.Properties;

namespace ColorVision.Engine.Archive.Dao
{
    [SugarTable("t_scgd_sys_config_rc")]
    public class SysConfigRcModel : ViewEntity 
    {
        [LocalizedDisplayName(nameof(Resources.Code))]
        public string Code { get; set; }

        [SugarColumn(ColumnName ="name"), LocalizedDisplayName(nameof(Resources.RCName))]
        public string Name { get; set; }

        [SugarColumn(ColumnName ="platform")]
        public string Platform { get; set; }

        [SugarColumn(ColumnName ="rest_address"), LocalizedDisplayName(nameof(Resources.RESTAddress))]
        public string RestAddress { get; set; }

        [SugarColumn(ColumnName ="version")]
        public string Version { get; set; }

        [SugarColumn(ColumnName ="token_expires")]
        public int? TokenExpires { get; set; }

        [SugarColumn(ColumnName ="admin_authorization")]
        public string AdminAuthorization { get; set; }

        [SugarColumn(ColumnName ="monitor_id"), LocalizedDisplayName(nameof(Resources.ServerMonitorConfig))]
        public int? MonitorId { get; set; }

        [SugarColumn(ColumnName ="archived_id"), LocalizedDisplayName(nameof(Resources.ArchiveConfiguration))]
        public int? ArchivedId { get; set; }

        [SugarColumn(ColumnName ="mqtt_cfg_id"), LocalizedDisplayName(nameof(Resources.MqttConfigId))]
        public int? MqttCfgId { get; set; }

        [SugarColumn(ColumnName ="mqtt_is_server")]
        public bool MqttIsServer { get; set; }

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; }

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName ="tenant_id"), LocalizedDisplayName(nameof(Resources.TenantId))]
        public int? TenantId { get; set; }

        [SugarColumn(ColumnName ="create_date"), LocalizedDisplayName(nameof(Resources.CreationDate))]
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
}
