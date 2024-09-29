using ColorVision.Engine.MySql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.POI.CADMapping
{
    public class MysqlCADMapping:IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlCADMapping";
        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `pid`, `mod_type`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (33, 'POI.CADMapping', 'CAD Mapping', NULL, 7, '2024-08-16 15:27:12', 1, 0, NULL, 0);";
            string t_scgd_sys_dictionary_mod_item = "INSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4330, 'MarginLeft', 4330, '布点左边距', 0, NULL, '0', 33, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4331, 'MarginTop', 4331, '布点上边距', 0, NULL, '0', 33, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4332, 'MarginRight', 4332, '布点右边距', 0, NULL, '0', 33, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4333, 'MarginBottom', 4333, '布点下边距', 0, NULL, '0', 33, '2023-11-14 17:44:15', 1, 0, NULL);\r\nINSERT INTO `t_scgd_sys_dictionary_mod_item` (`id`, `symbol`, `address_code`, `name`, `val_type`, `value_range`, `default_val`, `pid`, `create_date`, `is_enable`, `is_delete`, `remark`) VALUES (4334, 'MarginType', 4334, '布点边距类型', 4, NULL, 'Relative', 33, '2024-01-29 15:26:12', 1, 0, 'Relative/Absolute');";
            return t_scgd_sys_dictionary_mod_master + t_scgd_sys_dictionary_mod_item;
        }
    }
}
