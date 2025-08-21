using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.FindCross
{
    public class MysqlFindCross : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlFindCross";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`, `code`, `name`, `p_type`, `pid` , `mod_type`, `cfg_json`, `version`, `create_date`, `is_enable` , `is_delete`, `remark`, `tenant_id`) VALUES (45, 'FindCross', '十字计算', 1, NULL , 7, '{\"caclWay\": 1, \"debugCfg\": {\"Debug\": false, \"debugPath\": \"Result\\\\\\\\\", \"debugImgResize\": 2}, \"CheckLine\": {\"rho\": 5, \"houghV\": 100, \"floAngle\": 10}, \"threshold\": 25, \"blurKernel\": 3, \"maxLineGap\": 40, \"mathMaskRect\": {\"h\": 0, \"w\": 0, \"x\": 0, \"y\": 0, \"enable\": false}, \"opticsParams\": {\"stdCenter\": {\"x\": 0, \"y\": 0}, \"focusLength\": 14.5, \"sensorPixSize\": 3.76, \"objectDistance\": 500}, \"erodeAndDiate\": {\"erodeTime\": 0, \"dilateTime\": 0, \"erodeFirst\": true, \"erodeKernel\": 3, \"dilateKernel\": 3}, \"minLineLength\": 120, \"findEndPointWay\": 1, \"binaryByContours\": true, \"singleErodeKernel\": 15, \"binaryRateInContours\": 0.75}', '1.0', '2025-07-22 14:34:48', 1 , 0, NULL, 0) ON DUPLICATE KEY UPDATE `code` = VALUES(`code`), `name` = VALUES(`name`), `p_type` = VALUES(`p_type`), `pid` = VALUES(`pid`), `mod_type` = VALUES(`mod_type`) , `cfg_json` = VALUES(`cfg_json`), `version` = VALUES(`version`), `create_date` = VALUES(`create_date`), `is_enable` = VALUES(`is_enable`), `is_delete` = VALUES(`is_delete`) , `remark` = VALUES(`remark`), `tenant_id` = VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
