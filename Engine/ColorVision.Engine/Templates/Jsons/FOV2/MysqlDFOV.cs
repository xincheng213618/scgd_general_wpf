using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public class MysqlDFOV : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复FOV_2.0";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `create_date`, `is_enable`, `is_delete` , `remark`, `tenant_id`) VALUES (39, 'FOV_qk', 'DFOV', 1, NULL , 7, '{\"debug\": false, \"FovDist\": 9576, \"pattern\": 0, \"AnglesFov\": true, \"DarkRatio\": 0.5, \"debugPath\": \"result\\\\\\\\\", \"threshold\": 20000, \"ExactCorner\": {\"edge\": 10, \"cutWidth\": 200, \"qualityLevel\": 0.04}, \"VerticalFov\": true, \"HorizontalFov\": true, \"cameraDegrees\": 137}', '2024-04-28 11:38:32', 1, 0 , NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`), `remark` = VALUES(`remark`) , `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
