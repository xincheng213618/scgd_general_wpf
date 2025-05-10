using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.BinocularFusion
{
    public class TemplateBinocularFusion : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateBinocularFusion()
        {
            Title = "双目融合模板管理";
            Code = "ARVR.BinocularFusion";
            TemplateDicId = 35;
            TemplateParams = Params;
            IsUserControl = true;
        }

        public override void SetUserControlDataContext(int index)
        {
            EditTemplateJson.SetParam(TemplateParams[index].Value);
        }
        public EditTemplateJson EditTemplateJson { get; set; }

        public override UserControl GetUserControl()
        {
            EditTemplateJson = EditTemplateJson ?? new EditTemplateJson(Description);
            return EditTemplateJson;
        }
        public string Description { get; set; } = "   public struct BFConfig\r\n    {\r\n        //float center_x; //光学中心坐标，设为（0,0）表示参数不存在，以CMOS中心作为光学中心\r\n        //float center_y; //光学中心坐标，设为（0,0）表示参数不存在，以CMOS中心作为光学中心\r\n        public BFDPOINT center_pt; //光学中心坐标，设为（0,0）表示参数不存在，以CMOS中心作为光学中心\r\n        public float focus_length;    //单位mm\r\n        public float distance_XR_target_image; //单位mm，不明确XR虚像距时，设为0；\r\n        public float size_coms_pixel; //单位um\r\n        public double threshold_binary; // 255灰阶下的灰度阈值，也可以考虑Otsu阈值（鲁棒性未知）\r\n        public int knl_size_smooth;    // 均值平滑大小，为了把两线/亮条连成一片，推荐参数为5；\r\n        public int knl_size_erode ;    // 1pixel系列图推荐值21,9pixels系列图推荐值125；\r\n        public int crossMarks;   // 该参数暂时不起作用，目前只考虑找 中心、上、下、左、右 5个点的情况，所以暂时强制为5\r\n        public int min_corssMark_distancn { get; set; }\r\n        public float thh_ratio_hor;//水平\r\n        public float thh_ratio_vert;//水平\r\n\r\n        public string debugOutPath;\r\n    };";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);

        public override IMysqlCommand? GetMysqlCommand() => new MysqBinocularFusion();

    }




}
