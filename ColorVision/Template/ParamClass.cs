using ColorVision.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Template
{

    public class ParamBase:ViewModelBase
    {
        public event EventHandler IsEnabledChanged;

        [JsonProperty("enable")]
        [Category("_Setting"), DisplayName("是否启用")]
        public bool IsEnable { get => _IsEnable; set {
                if (IsEnable == value) return;
                _IsEnable = value; 
                if (value == true) IsEnabledChanged?.Invoke(this, new EventArgs()); 
                NotifyPropertyChanged(); } 
        }
        private bool _IsEnable;
    }

    public class AoiParam: ParamBase
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


}
