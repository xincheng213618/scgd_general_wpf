﻿using ColorVision.Engine.Archive.Dao;
using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;

namespace ColorVision.Engine.Sys.Dao
{
    [Table("t_scgd_sys_config_rc",PrimaryKey = "code")]
    public class SysConfigRcModel : VPKModel
    {

        [Column("code"), DisplayName("Code")]
        public string Code { get; set; }

        [Column("name"), DisplayName("RC名称")]
        public string Name { get; set; }

        [Column("platform")]
        public string Platform { get; set; }

        [Column("rest_address"), DisplayName("REST地址")]
        public string RestAddress { get; set; }

        [Column("version")]
        public string Version { get; set; }

        [Column("token_expires")]
        public int? TokenExpires { get; set; }

        [Column("admin_authorization")]
        public string AdminAuthorization { get; set; }

        [Column("monitor_id"), DisplayName("服务监控配置")]
        public int? MonitorId { get; set; }

        [Column("archived_id"), DisplayName("归档配置")]
        public int? ArchivedId { get; set; }

        [Column("mqtt_cfg_id"), DisplayName("MQTT配置ID")]
        public int? MqttCfgId { get; set; }

        [Column("mqtt_is_server")]
        public bool MqttIsServer { get; set; }

        [Column("is_enable")]
        public bool IsEnable { get; set; }

        [Column("is_delete")]
        public bool IsDelete { get; set; }

        [Column("tenant_id"), DisplayName("租户ID")]
        public int? TenantId { get; set; }

        [Column("create_date"), DisplayName("创建日期")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [Column("remark")]
        public string Remark { get; set; }
    }


    public class SysConfigRcDao : BaseTableDao<SysConfigRcModel>
    {
        public static SysConfigRcDao Instance { get; set; } = new SysConfigRcDao();

        public SysConfigRcModel? GetByCode(string code)
        {
            return GetByParam(new Dictionary<string, object>() { { "code", code } });
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
