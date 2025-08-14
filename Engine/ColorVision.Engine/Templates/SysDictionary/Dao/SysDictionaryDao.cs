using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.SysDictionary
{
    [SugarTable("t_scgd_sys_dictionary")]
    public class SysDictionaryModel : VPKModel
    {
        [Column("name")]
        public string? Name { get; set; }

        [Column("key")]
        public string? Key { get; set; }

        [Column("pid")]
        public int Type { get; set; }

        [Column("pid")]
        public int Pid { get; set; }
        [Column("val")]
        public int Value { get; set; }

        [Column("tenant_id")]
        public int TenantId { get; set; }

        [Column("is_enable")]
        public bool IsEnable { get; set; }

        [Column("is_delete")]
        public bool IsDelete { get; set; }

        [Column("is_hide")]
        public bool IsHide { get; set; }

        [Column("remark")]
        public string? Remark { get; set; }

    }
    public class SysDictionaryDao : BaseTableDao<SysDictionaryModel>
    {
        public static SysDictionaryDao Instance { get; set; } = new SysDictionaryDao();

        public int? GetPid(string Key)
        {
            SysDictionaryModel sysDictionaryModel = GetByParam(new Dictionary<string, object>() { { "key", Key } });
            return sysDictionaryModel != null ? sysDictionaryModel.Pid : null;
        }

    }
}
