using ColorVision.Engine.Interfaces;
using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.Matching
{
    [Table("t_scgd_algorithm_result_detail_aoi", PrimaryKey = "id")]
    public class AlgResultAoiModel : VPKModel, IViewResult
    {
        [Column("pid")]
        public int Pid { get; set; }

        [Column("score")]
        public float Score { get => _Score; set { _Score = value; NotifyPropertyChanged(); } }
        private float _Score;

        [Column("angle")]
        public float Angle { get => _Angle; set { _Angle = value; NotifyPropertyChanged(); } }
        private float _Angle;

        [Column("center_x")]
        public float CenterX { get => _CenterX; set { _CenterX = value; NotifyPropertyChanged(); } }
        private float _CenterX;


        [Column("center_y")]
        public float CenterY { get => _CenterY; set { _CenterY = value; NotifyPropertyChanged(); } }
        private float _CenterY;

        [Column("left_top_x")]
        public float LeftTopX { get => _LeftTopX; set { _LeftTopX = value; NotifyPropertyChanged(); } }
        private float _LeftTopX;

        [Column("left_top_y")]
        public float LeftTopY { get => _LeftTopY; set { _LeftTopY = value; NotifyPropertyChanged(); } }
        private float _LeftTopY;

        [Column("right_top_x")]
        public float RightTopX { get => _RightTopX; set { _RightTopX = value; NotifyPropertyChanged(); } }
        private float _RightTopX;

        [Column("right_top_y")]
        public float RightTopY { get => _RightTopY; set { _RightTopY = value; NotifyPropertyChanged(); } }
        private float _RightTopY;

        [Column("right_bottom_x")]
        public float RightBottomX { get => _RightBottomX; set { _RightBottomX = value; NotifyPropertyChanged(); } }
        private float _RightBottomX;

        [Column("right_bottom_y")]
        public float RightBottomY { get => _RightBottomY; set { _RightBottomY = value; NotifyPropertyChanged(); } }
        private float _RightBottomY;

        [Column("left_bottom_x")]
        public float LeftBottomX { get => _LeftBottomX; set { _LeftBottomX = value; NotifyPropertyChanged(); } }
        private float _LeftBottomX;

        [Column("left_bottom_y")]
        public float LeftBottomY { get => _LeftBottomY; set { _LeftBottomY = value; NotifyPropertyChanged(); } }
        private float _LeftBottomY;

    }


    public class AlgResultAoiDao : BaseTableDao<AlgResultAoiModel>
    {
        public static AlgResultAoiDao Instance { get; set; } = new AlgResultAoiDao();

        public AlgResultAoiDao() : base("t_scgd_algorithm_result_detail_aoi")
        {
        }
    }
}
