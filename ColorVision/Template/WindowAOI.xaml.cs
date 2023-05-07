#pragma warning disable CA1707
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ColorVision.Template
{
    //Aoi检测部分
    public class AoiParam
    {
        public bool filter_by_area { set; get; }
        public int max_area { set; get; }
        public int min_area { set; get; }
        public bool filter_by_contrast { set; get; }
        public float max_contrast { set; get; }
        public float min_contrast { set; get; }
        public float contrast_brightness { set; get; }
        public float contrast_darkness { set; get; }
        public int blur_size { set; get; }
        public int min_contour_size { set; get; }
        public int erode_size { set; get; }
        public int dilate_size { set; get; }
        [CategoryAttribute("AoiRect")]
        public int left { set; get; }
        [CategoryAttribute("AoiRect")]
        public int right { set; get; }
        [CategoryAttribute("AoiRect")]
        public int top { set; get; }
        [CategoryAttribute("AoiRect")]
        public int bottom { set; get; }
    };


    /// <summary>
    /// WindowAOI.xaml 的交互逻辑
    /// </summary>
    public partial class WindowAOI : Window
    {
        
        public WindowAOI()
        {
            InitializeComponent();
        }
    }
}
