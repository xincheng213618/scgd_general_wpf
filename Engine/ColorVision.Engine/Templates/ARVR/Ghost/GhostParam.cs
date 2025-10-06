using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.Engine.Templates.Ghost
{

    public class GhostParam : ParamModBase
    {

        public GhostParam() { }
        public GhostParam(ModMasterModel modMaster, List<ModDetailModel> modDetails) : base(modMaster, modDetails)
        {
        }

        [Category("Ghost"), Description("待检测鬼影点阵的半径长度(像素)")]
        public int Ghost_radius { get => GetValue(_Ghost_radius); set { SetProperty(ref _Ghost_radius, value); } }
        private int _Ghost_radius = 65;


        [Category("Ghost"), Description("待检测鬼影点阵的列数")]
        public int Ghost_cols { get => GetValue(_Ghost_cols); set { SetProperty(ref _Ghost_cols, value); } }
        private int _Ghost_cols = 3;
        [Category("Ghost"), Description("待检测鬼影点阵的行数")]
        public int Ghost_rows { get => GetValue(_Ghost_rows); set { SetProperty(ref _Ghost_rows, value); } }
        private int _Ghost_rows = 3;
        [Category("Ghost"), Description("待检测鬼影的中心灰度百分比上限")]
        public float Ghost_ratioH { get => GetValue(_Ghost_ratioH); set { SetProperty(ref _Ghost_ratioH, value); } }
        private float _Ghost_ratioH = 0.4f;
        [Category("Ghost"), Description("待检测鬼影的中心灰度百分比下限")]
        public float Ghost_ratioL { get => GetValue(_Ghost_ratioL); set { SetProperty(ref _Ghost_ratioL, value); } }
        private float _Ghost_ratioL = 0.2f;
    }


}
