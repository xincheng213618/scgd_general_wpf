using ColorVision.Engine.Abstractions;
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
        public float Score { get => _Score; set { _Score = value; NotifyPropertyChanged(); } }
        private float _Score;

        [SugarColumn(ColumnName ="angle")]
        public float Angle { get => _Angle; set { _Angle = value; NotifyPropertyChanged(); } }
        private float _Angle;

        [SugarColumn(ColumnName ="center_x")]
        public float CenterX { get => _CenterX; set { _CenterX = value; NotifyPropertyChanged(); } }
        private float _CenterX;


        [SugarColumn(ColumnName ="center_y")]
        public float CenterY { get => _CenterY; set { _CenterY = value; NotifyPropertyChanged(); } }
        private float _CenterY;

        [SugarColumn(ColumnName ="left_top_x")]
        public float LeftTopX { get => _LeftTopX; set { _LeftTopX = value; NotifyPropertyChanged(); } }
        private float _LeftTopX;

        [SugarColumn(ColumnName ="left_top_y")]
        public float LeftTopY { get => _LeftTopY; set { _LeftTopY = value; NotifyPropertyChanged(); } }
        private float _LeftTopY;

        [SugarColumn(ColumnName ="right_top_x")]
        public float RightTopX { get => _RightTopX; set { _RightTopX = value; NotifyPropertyChanged(); } }
        private float _RightTopX;

        [SugarColumn(ColumnName ="right_top_y")]
        public float RightTopY { get => _RightTopY; set { _RightTopY = value; NotifyPropertyChanged(); } }
        private float _RightTopY;

        [SugarColumn(ColumnName ="right_bottom_x")]
        public float RightBottomX { get => _RightBottomX; set { _RightBottomX = value; NotifyPropertyChanged(); } }
        private float _RightBottomX;

        [SugarColumn(ColumnName ="right_bottom_y")]
        public float RightBottomY { get => _RightBottomY; set { _RightBottomY = value; NotifyPropertyChanged(); } }
        private float _RightBottomY;

        [SugarColumn(ColumnName ="left_bottom_x")]
        public float LeftBottomX { get => _LeftBottomX; set { _LeftBottomX = value; NotifyPropertyChanged(); } }
        private float _LeftBottomX;

        [SugarColumn(ColumnName ="left_bottom_y")]
        public float LeftBottomY { get => _LeftBottomY; set { _LeftBottomY = value; NotifyPropertyChanged(); } }
        private float _LeftBottomY;

    }


    public class AlgResultAoiDao : BaseTableDao<AlgResultAoiModel>
    {
        public static AlgResultAoiDao Instance { get; set; } = new AlgResultAoiDao();

    }
}
