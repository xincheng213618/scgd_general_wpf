using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.CompoundImg
{
    public class MysqlCompoundImg : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复CompoundImg";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (46, 'CompoundImg', '图像拼接', 1, NULL, 7, '{\"caclType\": 0, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"overlapPart\": 0.2}', NULL, '2025-10-21 10:45:39', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
