﻿using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [Table("t_scgd_mod_third_party_algorithms")]
    public class ModThirdPartyAlgorithmsModel : VPKModel
    {
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
        public bool? IsEnable { get; set; }

        [Column("is_delete")]
        public bool? IsDelete { get; set; }

        [Column("remark")]
        public string? Remark { get; set; }

        [Column("tenant_id")]
        public string? TenantId { get; set; }
    }

    public class ModThirdPartyAlgorithmsDao : BaseTableDao<ThirdPartyAlgorithmsModel>
    {
        public static ModThirdPartyAlgorithmsDao Instance { get; set; } = new ModThirdPartyAlgorithmsDao();

        public ModThirdPartyAlgorithmsDao() : base("t_scgd_mod_third_party_algorithms", "guid")
        {

        }
    }
}