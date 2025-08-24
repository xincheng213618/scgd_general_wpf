using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.SFRFindROI
{
    public class MysqlSFRFindROI : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复MysqlSFRFindROI";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO t_scgd_sys_dictionary_mod_master (id, code, name, p_type, pid , mod_type, cfg_json, create_date, is_enable, is_delete , remark, tenant_id) VALUES (36, 'ARVR.SFR.FindROI', 'SFR寻边', 1, NULL , 7, '{\"th\": 0.6, \"roi_h\": 60, \"roi_w\": 60, \"minLength\": 100, \"lowThreshold\": 20, \"highThreshold\": 40}', '2024-11-05 11:25:17', 1, 0 , NULL, 0) ON DUPLICATE KEY UPDATE code = VALUES(code), name = VALUES(name), p_type = VALUES(p_type), pid = VALUES(pid), mod_type = VALUES(mod_type) , cfg_json = VALUES(cfg_json), create_date = VALUES(create_date), is_enable = VALUES(is_enable), is_delete = VALUES(is_delete), remark = VALUES(remark) , tenant_id = VALUES(tenant_id);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
