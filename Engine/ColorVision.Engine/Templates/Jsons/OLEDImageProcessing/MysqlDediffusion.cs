using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.OLEDImageProcessing
{
    public class MysqlDediffusion : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复解串扰";

        public string GetRecover()
        {
            return """
INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid`, `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable`, `is_delete`, `remark`, `tenant_id`) VALUES (202, 'OLED.Dediffusion', '解串扰', 1, NULL, 7, '{"rebuildCfg": {"de_kernel": [0.005801945726846804, 0.006259136699992473, 0.007446957706026484, 0.006259136699992473, 0.005801945726846804, 0.0067489050808179005, 0.01613838260967959, 0.05531039330231698, 0.01613838260967959, 0.0067489050808179005, 0.011469772733238114, 0.07160011470190694, 0.5685520426436761, 0.07160011470190694, 0.011469772733238114, 0.0067489050808179005, 0.01613838260967959, 0.05531039330231698, 0.01613838260967959, 0.0067489050808179005, 0.005801945726846804, 0.006259136699992473, 0.007446957706026484, 0.006259136699992473, 0.005801945726846804], "de_isotropic": 1, "de_steplength": 8, "de_defusion_en": true, "de_iterationlimit": 300, "de_totalerrorratio": 0.1, "de_kernel_size_cols": 5, "de_kernel_size_rows": 5}}', NULL, '2026-07-09 10:01:38', 1, 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`), `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);
""";
        }
    }
}
