using ColorVision.Engine.Abstractions;
using ColorVision.Engine.MySql.ORM;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Compliance
{
    [Table("t_scgd_algorithm_result_detail_compliance_xyz")]
    public class ComplianceXYZModel : PKModel, IViewResult
    {
        [Column("pid")]
        public int PId { get; set; }
        [Column("name")]
        public string Name { get; set; }
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


        public ObservableCollection<ValidateRuleResult>? ValidateSingles
        {
            get
            {
                if (ValidateResult == null) return null;
                return JsonConvert.DeserializeObject<ObservableCollection<ValidateRuleResult>>(ValidateResult);
            }
        }
        public bool Validate
        {
            get
            {
                if (ValidateSingles == null)
                    return false;
                bool result = true;
                foreach (var item in ValidateSingles)
                {
                    result = result && item.Result == ValidateRuleResultType.M;
                }
                return result;
            }
        }
    }



}
