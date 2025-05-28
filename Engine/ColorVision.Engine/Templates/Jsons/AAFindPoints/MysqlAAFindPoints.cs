using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.AAFindPoints
{
    public class MysqlAAFindPoints : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复AA发光区检测";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (42, 'FindLightArea', 'AA发光区检测', 1, NULL , 7, '{\"pattern\": 1, \"MaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"erodeTime\": 1, \"nummber_x\": 3, \"nummber_y\": 3, \"threshold\": 10000, \"brightRate\": 0.1, \"offset_top\": 0, \"ExactCorner\": {\"edge\": 10, \"cutWidth\": 400, \"qualityLevel\": 0.04}, \"erodeKernel\": 3, \"offset_left\": 0, \"offset_right\": 0, \"offset_bottom\": 0}', 'AA', '2024-04-28 11:38:32', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
