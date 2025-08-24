using ColorVision.Database;
using ColorVision.Database;
using NPOI.SS.Formula.Functions;
using SqlSugar;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_measure_result_third_party_algorithm")]
    public class ThirdPartyAlgorithmsResultModel : VPKModel, IInitTables
    {

        [SugarColumn(ColumnName ="img_file")]
        public string? ImageFilePath { get => _ImageFilePath; set { _ImageFilePath = value; NotifyPropertyChanged(); } }
        private string? _ImageFilePath;

        [SugarColumn(ColumnName ="result_type")]
        public int? ResultType { get => _ResultType; set { _ResultType = value; NotifyPropertyChanged(); } }
        private int? _ResultType;

        [SugarColumn(ColumnName ="input_param")]
        public string? InputParam { get => _InputParam; set { _InputParam = value; NotifyPropertyChanged(); } }
        private string? _InputParam;

        [SugarColumn(ColumnName ="dyna_param")]
        public string? DynamicParam { get => _DynamicParam; set { _DynamicParam = value; NotifyPropertyChanged(); } }
        private string? _DynamicParam;

        [SugarColumn(ColumnName ="batch_id")]
        public int? BatchId { get => _BatchId; set { _BatchId = value; NotifyPropertyChanged(); } }
        private int? _BatchId;
    }

    public class ThirdPartyAlgorithmsResultDao : BaseTableDao<ThirdPartyAlgorithmsResultModel>
    {
        public static ThirdPartyAlgorithmsResultDao Instance { get; set; } = new ThirdPartyAlgorithmsResultDao();

    }
}
