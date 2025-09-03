using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.PoiAnalysis
{
    public class MysqlPoiAnalysis : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复PoiAnalysis";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (48, 'MTF', 'MTF_V2', 1, NULL , 7, '{\"nV1\": {\"offsetX\": 0, \"offsetY\": 0, \"AAminSize\": 20, \"lineWidth\": 4, \"rectWidth\": 15, \"firstIsHor\": true, \"rectHeight\": 10, \"distanceToRect\": 20}, \"dRatio\": 0.01, \"pattern\": 2, \"RoiRects\": [{\"h\": 238, \"w\": 350, \"x\": 3415, \"y\": 2158}, {\"h\": 154, \"w\": 231, \"x\": 5118, \"y\": 2556}, {\"h\": 417, \"w\": 455, \"x\": 3492, \"y\": 3102}, {\"h\": 460, \"w\": 631, \"x\": 5203, \"y\": 3626}], \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"threshold\": 10000, \"CalcMethod\": 1, \"mathMaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}}', '2.0', '2025-05-26 14:31:59', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
