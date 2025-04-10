using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;
using CVCommCore;
using Newtonsoft.Json;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Compliance
{
    [Table("t_scgd_algorithm_result_detail_compliance_y")]
    public class ComplianceYModel : PKModel, IViewResult
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
