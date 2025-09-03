using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.BuildPOIAA
{
    public class MysqlAAFindPoints : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复AA布点";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (41, 'BuildPOI', 'AA布点', 1, NULL , 7, '{\"pattern\": 1, \"MaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}, \"RectArea\": {\"nummber_x\": 5, \"nummber_y\": 5, \"offset_top\": 0, \"offset_left\": 0, \"offset_right\": 0, \"offset_bottom\": 0}, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\", \"debugImgResize\": 2}, \"threshold\": 1800, \"CircleArea\": {\"turnNum\": 1, \"distance\": 400, \"roateAngle\": 0, \"singleAngle\": 36}, \"brightRate\": 0.3, \"ExactCorner\": {\"edge\": 4, \"active\": true, \"cutWidth\": 200, \"qualityLevel\": 0.04}, \"aaLocationWay\": 1, \"erodeAndDiate\": {\"erodeTime\": 0, \"dilateTime\": 2, \"erodeFirst\": false, \"erodeKernel\": 3, \"dilateKernel\": 3}}', 'AA', '2024-04-28 11:38:32', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
