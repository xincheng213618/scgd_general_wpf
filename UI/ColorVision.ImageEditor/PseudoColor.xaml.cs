﻿using ColorVision.Common.MVVM;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public class PseudoValue : ViewModelBase
    {
        public string ValText { get => _ValText; set { _ValText = value; NotifyPropertyChanged(); } }
        private string _ValText;

        public SolidColorBrush Color { get => _Color; set { _Color = value; NotifyPropertyChanged(); } }
        private SolidColorBrush _Color;
    }

    /// <summary>
    /// PseudoColor.xaml 的交互逻辑
    /// </summary>
    public partial class PseudoColor : Window
    {
        double MinMax { get; set; }
        double MinData { get; set; }
        double MaxData { get; set; }= 255;


        public ObservableCollection<PseudoValue> PseudoValues { get; set; }

        public ImageViewConfig Config { get; set; }
        public PseudoColor(ImageViewConfig config)
        {
            Config = config;
            PseudoValues = new ObservableCollection<PseudoValue>();
            InitializeComponent();
            this.ApplyCaption();
        }


        public static Dictionary<ColormapTypes, string> GetColormapsDictionary()
        {
            var colormapDictionary = new Dictionary<ColormapTypes, string>
        {
            { ColormapTypes.COLORMAP_AUTUMN, "Assets/Colormaps/colorscale_autumn.jpg" },
            { ColormapTypes.COLORMAP_BONE, "Assets/Colormaps/colorscale_bone.jpg" },
            { ColormapTypes.COLORMAP_JET, "Assets/Colormaps/colorscale_jet.jpg" },
            { ColormapTypes.COLORMAP_WINTER, "Assets/Colormaps/colorscale_winter.jpg" },
            { ColormapTypes.COLORMAP_RAINBOW, "Assets/Colormaps/colorscale_rainbow.jpg" },
            { ColormapTypes.COLORMAP_OCEAN, "Assets/Colormaps/colorscale_ocean.jpg" },
            { ColormapTypes.COLORMAP_SUMMER, "Assets/Colormaps/colorscale_summer.jpg" },
            { ColormapTypes.COLORMAP_SPRING, "Assets/Colormaps/colorscale_spring.jpg" },
            { ColormapTypes.COLORMAP_COOL, "Assets/Colormaps/colorscale_cool.jpg" },
            { ColormapTypes.COLORMAP_HSV, "Assets/Colormaps/colorscale_hsv.jpg" },
            { ColormapTypes.COLORMAP_PINK, "Assets/Colormaps/colorscale_pink.jpg" },
            { ColormapTypes.COLORMAP_HOT, "Assets/Colormaps/colorscale_hot.jpg" },
            { ColormapTypes.COLORMAP_PARULA, "Assets/Colormaps/colorscale_parula.jpg" },
            { ColormapTypes.COLORMAP_MAGMA, "Assets/Colormaps/colorscale_magma.jpg" },
            { ColormapTypes.COLORMAP_INFERNO, "Assets/Colormaps/colorscale_inferno.jpg" },
            { ColormapTypes.COLORMAP_PLASMA, "Assets/Colormaps/colorscale_plasma.jpg" },
            { ColormapTypes.COLORMAP_VIRIDIS, "Assets/Colormaps/colorscale_viridis.jpg" },
            { ColormapTypes.COLORMAP_CIVIDIS, "Assets/Colormaps/colorscale_cividis.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT, "Assets/Colormaps/colorscale_twilight.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT_SHIFTED, "Assets/Colormaps/colorscale_twilight_shifted.jpg" },
            { ColormapTypes.COLORMAP_TURBO, "Assets/Colormaps/colorscale_turbo.jpg" },
            { ColormapTypes.COLORMAP_DEEPGREEN, "Assets/Colormaps/colorscale_deepgreen.jpg" }
        };
            return colormapDictionary;
        }

        public static Dictionary<ColormapTypes, string> GetColormapDictionary()
        {
            var colormapDictionary = new Dictionary<ColormapTypes, string>
        {
            { ColormapTypes.COLORMAP_AUTUMN, "Assets/Colormap/colorscale_autumn.jpg" },
            { ColormapTypes.COLORMAP_BONE, "Assets/Colormap/colorscale_bone.jpg" },
            { ColormapTypes.COLORMAP_JET, "Assets/Colormap/colorscale_jet.jpg" },
            { ColormapTypes.COLORMAP_WINTER, "Assets/Colormap/colorscale_winter.jpg" },
            { ColormapTypes.COLORMAP_RAINBOW, "Assets/Colormap/colorscale_rainbow.jpg" },
            { ColormapTypes.COLORMAP_OCEAN, "Assets/Colormap/colorscale_ocean.jpg" },
            { ColormapTypes.COLORMAP_SUMMER, "Assets/Colormap/colorscale_summer.jpg" },
            { ColormapTypes.COLORMAP_SPRING, "Assets/Colormap/colorscale_spring.jpg" },
            { ColormapTypes.COLORMAP_COOL, "Assets/Colormap/colorscale_cool.jpg" },
            { ColormapTypes.COLORMAP_HSV, "Assets/Colormap/colorscale_hsv.jpg" },
            { ColormapTypes.COLORMAP_PINK, "Assets/Colormap/colorscale_pink.jpg" },
            { ColormapTypes.COLORMAP_HOT, "Assets/Colormap/colorscale_hot.jpg" },
            { ColormapTypes.COLORMAP_PARULA, "Assets/Colormap/colorscale_parula.jpg" },
            { ColormapTypes.COLORMAP_MAGMA, "Assets/Colormap/colorscale_magma.jpg" },
            { ColormapTypes.COLORMAP_INFERNO, "Assets/Colormap/colorscale_inferno.jpg" },
            { ColormapTypes.COLORMAP_PLASMA, "Assets/Colormap/colorscale_plasma.jpg" },
            { ColormapTypes.COLORMAP_VIRIDIS, "Assets/Colormap/colorscale_viridis.jpg" },
            { ColormapTypes.COLORMAP_CIVIDIS, "Assets/Colormap/colorscale_cividis.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT, "Assets/Colormap/colorscale_twilight.jpg" },
            { ColormapTypes.COLORMAP_TWILIGHT_SHIFTED, "Assets/Colormap/colorscale_twilight_shifted.jpg" },
            { ColormapTypes.COLORMAP_TURBO, "Assets/Colormap/colorscale_turbo.jpg" },
            { ColormapTypes.COLORMAP_DEEPGREEN, "Assets/Colormap/colorscale_deepgreen.jpg" }
        };
            return colormapDictionary;
        }

        private void ComColormapTypes_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            var ColormapTypes = GetColormapsDictionary().First(x => x.Value == ComColormapTypes.SelectedValue.ToString()).Key;
            string valuepath = ComColormapTypes.SelectedValue.ToString();
            ColormapTypesImage.Source = new BitmapImage(new Uri($"/ColorVision.ImageEditor;component/{valuepath}", UriKind.Relative));
        }

        private void UserControl_Initialized(object sender, EventArgs e)
        {
            this.DataContext = Config;
            ComColormapTypes.ItemsSource = GetColormapsDictionary();
        }

        private void button_Create_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }



        private void RangeSlider1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<HandyControl.Data.DoubleRange> e)
        {
            if (ActualWidth > 0)
            {
                RowDefinitionStart.Width = new GridLength(((ActualWidth - 100) / 255.0) * (255 - RangeSlider1.ValueEnd));
                RowDefinitionEnd.Width = new GridLength(((ActualWidth - 100) / 255.0) * RangeSlider1.ValueStart);
            }
            //获取当前的滚动条的start位置以及end位置
        }

        public void Genera()
        {
            double minLua = RangeSlider1.ValueStart;
            double maxLua = RangeSlider1.ValueEnd;
            //colormapNum = Int32.Parse(this.textBox_level.DisPlayName);
            init();
        }

        //初始化伪彩色的这个表
        private void init()
        {
            PseudoValues.Clear();
        }

        private void textBox_ValueChanged(object sender, HandyControl.Data.FunctionEventArgs<double> e)
        {
            Genera();
        }


    }
}

