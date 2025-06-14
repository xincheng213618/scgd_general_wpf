﻿using ColorVision.Engine.MySql.ORM;
using NPOI.SS.Formula.Functions;
using System.Data;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [Table("t_scgd_mod_third_party_algorithms")]
    public class ModThirdPartyAlgorithmsModel : VPKModel
    {
        [Column("pid")]
        public int? PId { get => _PId; set { _PId = value; NotifyPropertyChanged(); } }
        private int? _PId;

        [Column("code")]
        public string? Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string? _Code;

        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("json_val")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; NotifyPropertyChanged(); } }
        private string? _JsonVal;

        [Column("is_enable")]
        public bool? IsEnable { get; set; } = true;

        [Column("is_delete")]
        public bool? IsDelete { get; set; } = false;

        [Column("remark")]
        public string? Remark { get; set; }

        [Column("tenant_id")]
        public string? TenantId { get; set; }

    }

    public class ModThirdPartyAlgorithmsDao : BaseTableDao<ModThirdPartyAlgorithmsModel>
    {
        public static ModThirdPartyAlgorithmsDao Instance { get; set; } = new ModThirdPartyAlgorithmsDao();

        public override DataRow Model2Row(ModThirdPartyAlgorithmsModel item, DataRow row) => ReflectionHelper.Model2RowAuto(item, row);

    }
}
