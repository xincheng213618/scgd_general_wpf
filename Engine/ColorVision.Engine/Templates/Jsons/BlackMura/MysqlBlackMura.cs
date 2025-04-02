using ColorVision.Engine.MySql;

namespace ColorVision.Engine.Templates.Jsons.BlackMura
{
    public class MysqlBlackMura : IMysqlCommand
    {
        public string GetMysqlCommandName() => "恢复BlackMura";

        public string GetRecover()
        {
            string t_scgd_sys_dictionary_mod_master = "INSERT INTO t_scgd_sys_dictionary_mod_master (id, code, name, p_type, pid , mod_type, cfg_json, create_date, is_enable, is_delete , remark, tenant_id) VALUES (35, 'ARVR.BinocularFusion', '双目融合', 1, NULL , 7, '{\"m_de\": 23, \"n_de\": 23, \"aa_cut\": 10, \"rotate\": false, \"poi_type\": 0, \"resize_h\": 1080, \"resize_w\": 1920, \"aa_size_h\": 150, \"aa_size_w\": 300, \"display_h\": 1080, \"display_w\": 1920, \"poi_num_x\": 19, \"poi_num_y\": 10, \"erode_size\": 3, \"min_aa_area\": 1000, \"aa_threshold\": 0.1}', '2024-11-05 11:25:17', 1, 0 , NULL, 0) ON DUPLICATE KEY UPDATE code = VALUES(code), name = VALUES(name), p_type = VALUES(p_type), pid = VALUES(pid), mod_type = VALUES(mod_type) , cfg_json = VALUES(cfg_json), create_date = VALUES(create_date), is_enable = VALUES(is_enable), is_delete = VALUES(is_delete), remark = VALUES(remark) , tenant_id = VALUES(tenant_id);";
            return t_scgd_sys_dictionary_mod_master;
        }
    }
}
