﻿using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.FOV2
{
    public class MysqlFOV : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复FOV_2.0";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO `t_scgd_sys_dictionary_mod_master` (`id`,`code`,`name`,`p_type`,`pid`,`mod_type`,`cfg_json`,`version`,`create_date`,`is_enable`,`is_delete`,`remark`,`tenant_id`) VALUES (39,'FOV','FOV_V2',1,NULL,7,'{\\\"FovDist\\\":9576,\\\"pattern\\\":0,\\\"debugCfg\\\":{\\\"Debug\\\":false,\\\"debugPath\\\":\\\"Result\\\\\\\\\\\",\\\"debugImgResize\\\":2},\\\"AnglesFov\\\":true,\\\"DarkRatio\\\":0.5,\\\"threshold\\\":2000,\\\"ExactCorner\\\":{\\\"edge\\\":10,\\\"active\\\":true,\\\"cutWidth\\\":200,\\\"qualityLevel\\\":0.04},\\\"VerticalFov\\\":true,\\\"HorizontalFov\\\":true,\\\"aaLocationWay\\\":1,\\\"cameraDegrees\\\":137}','2.0','2024-04-28 11:38:32',1,0,NULL,0)\r\nON DUPLICATE KEY UPDATE\r\n`code`=VALUES(`code`),`name`=VALUES(`name`),`p_type`=VALUES(`p_type`),`pid`=VALUES(`pid`),`mod_type`=VALUES(`mod_type`),`cfg_json`=VALUES(`cfg_json`),`version`=VALUES(`version`),`create_date`=VALUES(`create_date`),`is_enable`=VALUES(`is_enable`),`is_delete`=VALUES(`is_delete`),`remark`=VALUES(`remark`),`tenant_id`=VALUES(`tenant_id`);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
