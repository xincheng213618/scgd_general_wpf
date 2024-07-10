using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Services.Devices.Algorithm.Templates.Compliance
{
    [Table("t_scgd_algorithm_result_detail_compliance_y")]
    public class ComplianceYModel : PKModel
    {
        [Column("pid")]
        public int PId { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("data_type")]
        public int DataType { get; set; }

        [Column("data_value")]
        public float DataValue { get; set; }

        [Column("validate_result")]
        public string? ValidateResult { get; set; }
    }



}
