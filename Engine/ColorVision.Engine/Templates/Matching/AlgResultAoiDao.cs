using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Templates.Matching
{
    [SugarTable("t_scgd_algorithm_result_detail_aoi")]
    public class AlgResultAoiModel : VPKModel, IViewResult
    {
        [SugarColumn(ColumnName ="pid")]
        public int Pid { get; set; }

        [SugarColumn(ColumnName ="score")]
        public float Score { get => _Score; set { _Score = value; OnPropertyChanged(); } }
        private float _Score;

        [SugarColumn(ColumnName ="angle")]
        public float Angle { get => _Angle; set { _Angle = value; OnPropertyChanged(); } }
        private float _Angle;

        [SugarColumn(ColumnName ="center_x")]
        public float CenterX { get => _CenterX; set { _CenterX = value; OnPropertyChanged(); } }
        private float _CenterX;


        [SugarColumn(ColumnName ="center_y")]
        public float CenterY { get => _CenterY; set { _CenterY = value; OnPropertyChanged(); } }
        private float _CenterY;

        [SugarColumn(ColumnName ="left_top_x")]
        public float LeftTopX { get => _LeftTopX; set { _LeftTopX = value; OnPropertyChanged(); } }
        private float _LeftTopX;

        [SugarColumn(ColumnName ="left_top_y")]
        public float LeftTopY { get => _LeftTopY; set { _LeftTopY = value; OnPropertyChanged(); } }
        private float _LeftTopY;

        [SugarColumn(ColumnName ="right_top_x")]
        public float RightTopX { get => _RightTopX; set { _RightTopX = value; OnPropertyChanged(); } }
        private float _RightTopX;

        [SugarColumn(ColumnName ="right_top_y")]
        public float RightTopY { get => _RightTopY; set { _RightTopY = value; OnPropertyChanged(); } }
        private float _RightTopY;

        [SugarColumn(ColumnName ="right_bottom_x")]
        public float RightBottomX { get => _RightBottomX; set { _RightBottomX = value; OnPropertyChanged(); } }
        private float _RightBottomX;

        [SugarColumn(ColumnName ="right_bottom_y")]
        public float RightBottomY { get => _RightBottomY; set { _RightBottomY = value; OnPropertyChanged(); } }
        private float _RightBottomY;

        [SugarColumn(ColumnName ="left_bottom_x")]
        public float LeftBottomX { get => _LeftBottomX; set { _LeftBottomX = value; OnPropertyChanged(); } }
        private float _LeftBottomX;

        [SugarColumn(ColumnName ="left_bottom_y")]
        public float LeftBottomY { get => _LeftBottomY; set { _LeftBottomY = value; OnPropertyChanged(); } }
        private float _LeftBottomY;

    }


    public class AlgResultAoiDao : BaseTableDao<AlgResultAoiModel>
    {
        public static AlgResultAoiDao Instance { get; set; } = new AlgResultAoiDao();

    }
}
