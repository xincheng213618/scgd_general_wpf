using ColorVision.Common.MVVM;
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.SysDictionary
{
    [Table("t_scgd_sys_dictionary_mod_master")]
    public class SysDictionaryModModel : ViewModelBase,IPKModel
    {
        [Column("id")]
        public int Id { get; set; } 
        [Column("pid")]
        public int? PId { get => _PId; set { _PId = value; NotifyPropertyChanged(); } }
        private int? _PId;
        [Column("mod_type")]
        public short ModType { get => _ModType; set { _ModType = value; NotifyPropertyChanged(); } }
        private short _ModType;

        [Column("code")]
        public string Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string _Code;

        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("tenant_id")]
        public int TenantId { get => _TenantId; set { _TenantId = value; NotifyPropertyChanged(); } }
        private int _TenantId;
        [Column("create_date")]
        public DateTime CreateDate { get => _CreateDate; set { _CreateDate = value; NotifyPropertyChanged(); } }
        private DateTime _CreateDate;
    }

    public class SysDictionaryModMasterDao : BaseTableDao<SysDictionaryModModel>
    {
        public static SysDictionaryModMasterDao Instance { get; set; } = new SysDictionaryModMasterDao();


        public SysDictionaryModModel? GetByCode(string code, int tenantId) => GetByParam( new Dictionary<string, object>() { { "is_delete",0 }, { "code", code }, { "tenant_id", tenantId } });
    }
}
