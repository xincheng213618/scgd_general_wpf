using ColorVision.Engine.MySql;
using System.Collections.ObjectModel;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates.Jsons.GhostQK
{
    public class TemplateGhostQK : ITemplateJson<TemplateJsonParam>, IITemplateLoad
    {
        public static ObservableCollection<TemplateModel<TemplateJsonParam>> Params { get; set; } = new ObservableCollection<TemplateModel<TemplateJsonParam>>();

        public TemplateGhostQK()
        {
            Title = "Ghost模板管理";
            Code = "ghost_qk";
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
        public string Description { get; set; } = "//blackmura 相关\r\nstruct BlackMuraParam\r\n{\r\n        float aa_threshold;  //阈值\r\n        int   erode_size;    //腐蚀去噪尺寸\r\n        int   min_aa_area;   //最小的发光区大小，判断pattern\r\n        int   aa_cut;  // aa区裁剪像素，负值表示外扩\r\n        int   display_w;  //pixel 屏幕分辨率\r\n        int   display_h;\r\n        double aa_size_w;  //aa区实际尺寸\r\n        double aa_size_h;\r\n        int   m_de;       //水平方向上的平均尺寸  ，显示器像素\r\n        int   n_de;       //垂直方向的平均尺寸\r\n        bool  rotate;     //是否旋转\r\n        int  poi_num_x;   //Uniformity 计算相关； block设置\r\n        int  poi_num_y;\r\n        int  poi_type;    //0，矩形 1 圆形\r\n};";

        public override UserControl CreateUserControl() => new EditTemplateJson(Description);
        public override IMysqlCommand? GetMysqlCommand() => new MysqlGhostQK();

    }




}
