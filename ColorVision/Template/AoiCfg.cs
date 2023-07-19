using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Template
{
    class AoiCfg
    {
        [Category("AOI配置"), TypeConverter(typeof(ExpandableObjectConverter))]
        public FindRoi FindRoi { set; get; }

        [Category("AOI配置")]
        public float contrastBrightness { set; get; }
        [Category("AOI配置")]
        public float contrastDarkness { set; get; }
        [Category("AOI配置")]
        public float maxContrast { set; get; }
        [Category("AOI配置")]
        public float minContrast { set; get; }
        [Category("AOI配置")]
        public bool filterByContrast { set; get; }
        [Category("AOI配置")]
        public int maxArea { set; get; }
        [Category("AOI配置")]
        public int minArea { set; get; }
        [Category("AOI配置")]
        public bool filterByArea { set; get; }
        [Category("AOI配置")]
        public int blurSize { set; get; }
        public AoiCfg()
        {

        }
    }
    class MuraCfg
    {
        [Category("mura配置")]
        public int smooth_size_mura_h { set; get; }
        [Category("mura配置")]
        public int smooth_size_mura_w { set; get; }
        [Category("mura配置")]
        public int detector_size_mura_h { set; get; }
        [Category("mura配置")]
        public int detector_size_mura_w { set; get; }

        [Category("mura配置")]
        public int smooth_size_fos_h { set; get; }
        [Category("mura配置")]
        public int smooth_size_fos_w { set; get; }
        [Category("mura配置")]
        public int detector_size_fos_h { set; get; }
        [Category("mura配置")]
        public int detector_size_fos_w { set; get; }
        [Category("mura配置")]
        public float threshold_LCR_dark_fos { set; get; }
        [Category("mura配置")]
        public int threshold_pixel_quantity_fos { set; get; }
        [Category("mura配置")]
        public int resize_scale { set; get; }
        [Category("mura配置")]
        public int binary_threshold { set; get; }
        [Category("mura配置")]
        public int additional_crop_x { set; get; }
        [Category("mura配置")]
        public int additional_crop_y { set; get; }
        [Category("mura配置")]
        public int additional_crop_bottom { set; get; }

        [Category("mura配置")]
        public int target_rows { set; get; }
        [Category("mura配置")]
        public int target_cols { set; get; }
        [Category("mura配置")]
        public bool vertical { set; get; }
        [Category("mura配置")]
        public int contour_color_b { set; get; }
        [Category("mura配置")]
        public int contour_color_g { set; get; }
        [Category("mura配置")]
        public int contour_color_r { set; get; }
        [Category("mura配置")]
        public int contour_color_fos_b { set; get; }
        [Category("mura配置")]
        public int contour_color_fos_g { set; get; }
        [Category("mura配置")]
        public int contour_color_fos_r { set; get; }
        [Category("mura配置")]
        public int thickness_draw_contour { set; get; }
        [Category("mura配置")]
        public int threshold_band_length { set; get; }

        [Category("mura配置")]
        public float basic_LCR { set; get; }
        [Category("mura配置")]
        public int min_blob_size { set; get; }
        [Category("mura配置")]
        public int max_blob_size { set; get; }
        [Category("mura配置")]
        public int min_maxLCR { set; get; }
        [Category("mura配置")]
        public int min_averageLCR { set; get; }

        [Category("mura配置")]
        public int min_aspect_ratio { set; get; }
        [Category("mura配置")]
        public int max_aspect_ratio { set; get; }
        [Category("mura配置")]
        public int core_LCR { set; get; }
        [Category("mura配置")]
        public int min_core_size_pixels { set; get; }
        [Category("mura配置")]
        public int min_orient_angle { set; get; }
        [Category("mura配置"), DisplayName("颜色")]
        public int max_orient_angle { set; get; }

    }
    class FindRoi
    {
        public int x { set; get; }
        public int y { set; get; }
        public int width { set; get; }

        public int height { set; get; }
        public override string ToString()
        {
            return string.Format("{0},{1},{2},{3}", x, y, width, height);
        }
    }
}

