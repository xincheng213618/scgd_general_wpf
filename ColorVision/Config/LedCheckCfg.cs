using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Config
{
    public class LedCheckCfg
    {
        [Category("灯珠检测配置"), Browsable(false)]
        public bool isburst { set; get; }
        [Category("灯珠检测配置")]
        public bool 是否使用本地点位信息计算 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public bool 是否使用本地Mark点计算 { set; get; }
        [Category("灯珠检测配置")]
        public string 本地点位信息坐标 { set; get; }
        [Category("灯珠检测配置")]
        public int 灯珠宽方向数量 { set; get; }
        [Category("灯珠检测配置")]
        public int 灯珠高方向数量 { set; get; }
        [Category("灯珠检测配置")]
        public int 计算图像格式 { set; get; }
        [Category("灯珠检测配置")]
        public double 轮廓范围系数 { set; get; }
        [Category("灯珠检测配置")]
        public int 图像二值化补正 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public double 亮度系数 { set; get; }
        [Category("灯珠检测配置")]
        public int 灯珠抓取通道 { set; get; }
        [Category("灯珠检测配置")]
        public bool isdebug { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public bool 是否使用本地图片计算 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public int 是否启用固定半径计算 { set; get; }
        [Category("灯珠检测配置")]
        public int 灯珠固定半径 { set; get; }
        [Category("灯珠检测配置")]
        public int 轮廓最小面积 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public string 四色校正地址 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public bool 是否下限检测 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public bool 是否上限检测 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public float 下限检测阈值百分比 { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public float 下限检测阈值固定值 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public float 上限检测阈值百分比 { set; get; }
        [Category("灯珠检测配置"), Browsable(false)]
        public float 上限检测阈值固定值 { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public bool 是否存本地原图 { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public int[] 关注范围 { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public int 关注区域二值化 { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public int boundry { set; get; }

        [Category("灯珠检测配置")]
        public double[] LengthCheck { set; get; }

        [Category("灯珠检测配置")]
        public double[] LengthRange { set; get; }

        [Category("灯珠检测配置"), Browsable(false)]
        public int 判断方式 { set; get; }

    }
}
