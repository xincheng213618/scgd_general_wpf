using ColorVision.Database;
using CVCommCore;
using Newtonsoft.Json;
using SqlSugar;
using System.Collections.ObjectModel;

namespace ColorVision.Engine.Templates.Compliance
{
    [SugarTable("t_scgd_algorithm_result_detail_compliance_jnd")]
    public class ComplianceJNDModel : EntityBase, IViewResult, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int PId { get; set; }

        [SugarColumn(ColumnName ="name")]
        public string Name { get; set; }

        [SugarColumn(ColumnName ="data_type")]
        public int DataType { get; set; }

        [SugarColumn(ColumnName ="data_val_h")]
        public float DataValueH { get; set; }

        [SugarColumn(ColumnName ="data_val_v")]
        public float DataValueV { get; set; }

        [SugarColumn(ColumnName ="validate_result")]
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


    public class ComplianceJNDDao : BaseTableDao<ComplianceJNDModel>
    {
        public static ComplianceJNDDao Instance { get; set; } = new ComplianceJNDDao();
    }

}
