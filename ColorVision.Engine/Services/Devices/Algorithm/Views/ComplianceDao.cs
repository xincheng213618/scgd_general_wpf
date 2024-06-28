using ColorVision.Engine.MySql.ORM;
using ColorVision.Engine.Services.Devices.Algorithm.Dao;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Algorithm.Views
{
    [Table("t_scgd_algorithm_result_detail_compliance_y")]
    public class ComplianceYModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("name")]
        public int Name { get; set; }

        [Column("data_type")]
        public int DataType { get; set; }

        [Column("data_value")]
        public float DataValue { get; set; }

        [Column("validate_result")]
        public string? ValidateResult { get; set; }
    }


    public class ComplianceYDao :BaseTableDao<ComplianceYModel>
    {
        public static ComplianceYDao Instance { get; set; } = new ComplianceYDao();
        public ComplianceYDao() : base("t_scgd_algorithm_result_detail_compliance_y")
        {
        }
    }

    public class ComplianceXYZDao : BaseTableDao<ComplianceXYZModel>
    {
        public static ComplianceXYZDao Instance { get; set; } = new ComplianceXYZDao();
        public ComplianceXYZDao() : base("t_scgd_algorithm_result_detail_compliance_xyz")
        {
        }
    }




    [Table("t_scgd_algorithm_result_detail_compliance_xyz")]
    public class ComplianceXYZModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }
        [Column("name")]
        public int Name { get; set; }
        [Column("data_type")]
        public int DataType { get; set; }

        [Column("data_value_x")]
        public float DataValuex { get; set; }
        [Column("data_value_y")]
        public float DataValuey { get; set; }
        [Column("data_value_z")]
        public float DataValuez { get; set; }

        [Column("data_value_u")]
        public float DataValueu { get; set; }

        [Column("data_value_v")]
        public float DataValuev { get; set; }

        [Column("data_value_yyy")]
        public float DataValueyyy { get; set; }
        [Column("data_value_xxx")]
        public float DataValuexxx { get; set; }
        [Column("data_value_zzz")]
        public float DataValuezzz { get; set; }
        [Column("data_value_cct")]
        public float DataValueCCT { get; set; }

        [Column("data_value_wave")]
        public float DataValueWave { get; set; }

        [Column("validate_result")]
        public string? ValidateResult { get; set; }
    }



}
