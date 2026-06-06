using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    internal sealed class DisplayShaderFilterEffect : ShaderEffect
    {
        private static readonly PixelShader Shader = new()
        {
            UriSource = new Uri("pack://application:,,,/ColorVision.ImageEditor;component/EditorTools/Filters/Shaders/DisplayShaderFilter.ps", UriKind.Absolute)
        };

        public static readonly DependencyProperty InputProperty =
            RegisterPixelShaderSamplerProperty(nameof(Input), typeof(DisplayShaderFilterEffect), 0);

        public static readonly DependencyProperty LutProperty =
            RegisterPixelShaderSamplerProperty(nameof(Lut), typeof(DisplayShaderFilterEffect), 1);

        public static readonly DependencyProperty ChannelModeProperty = RegisterShaderDouble(nameof(ChannelMode), 0, 0);
        public static readonly DependencyProperty RedGainProperty = RegisterShaderDouble(nameof(RedGain), 1, 1);
        public static readonly DependencyProperty GreenGainProperty = RegisterShaderDouble(nameof(GreenGain), 2, 1);
        public static readonly DependencyProperty BlueGainProperty = RegisterShaderDouble(nameof(BlueGain), 3, 1);
        public static readonly DependencyProperty RedOffsetProperty = RegisterShaderDouble(nameof(RedOffset), 4, 0);
        public static readonly DependencyProperty GreenOffsetProperty = RegisterShaderDouble(nameof(GreenOffset), 5, 0);
        public static readonly DependencyProperty BlueOffsetProperty = RegisterShaderDouble(nameof(BlueOffset), 6, 0);
        public static readonly DependencyProperty BrightnessProperty = RegisterShaderDouble(nameof(Brightness), 7, 0);
        public static readonly DependencyProperty ContrastProperty = RegisterShaderDouble(nameof(Contrast), 8, 1);
        public static readonly DependencyProperty GammaProperty = RegisterShaderDouble(nameof(Gamma), 9, 1);
        public static readonly DependencyProperty SaturationProperty = RegisterShaderDouble(nameof(Saturation), 10, 1);
        public static readonly DependencyProperty InvertProperty = RegisterShaderDouble(nameof(Invert), 11, 0);
        public static readonly DependencyProperty ThresholdModeProperty = RegisterShaderDouble(nameof(ThresholdMode), 12, 0);
        public static readonly DependencyProperty ThresholdProperty = RegisterShaderDouble(nameof(Threshold), 13, 0.5);
        public static readonly DependencyProperty ThresholdLowProperty = RegisterShaderDouble(nameof(ThresholdLow), 14, 0.05);
        public static readonly DependencyProperty ThresholdHighProperty = RegisterShaderDouble(nameof(ThresholdHigh), 15, 0.95);
        public static readonly DependencyProperty RangeLowProperty = RegisterShaderDouble(nameof(RangeLow), 16, 0.4);
        public static readonly DependencyProperty RangeHighProperty = RegisterShaderDouble(nameof(RangeHigh), 17, 0.6);
        public static readonly DependencyProperty HighlightOpacityProperty = RegisterShaderDouble(nameof(HighlightOpacity), 18, 0.75);
        public static readonly DependencyProperty PseudoColorModeProperty = RegisterShaderDouble(nameof(PseudoColorMode), 19, 0);
        public static readonly DependencyProperty PseudoMinProperty = RegisterShaderDouble(nameof(PseudoMin), 20, 0);
        public static readonly DependencyProperty PseudoMaxProperty = RegisterShaderDouble(nameof(PseudoMax), 21, 1);

        public DisplayShaderFilterEffect()
        {
            PixelShader = Shader;
            Lut = CreateLutBrush();

            UpdateShaderValue(InputProperty);
            UpdateShaderValue(LutProperty);
            UpdateShaderValue(ChannelModeProperty);
            UpdateShaderValue(RedGainProperty);
            UpdateShaderValue(GreenGainProperty);
            UpdateShaderValue(BlueGainProperty);
            UpdateShaderValue(RedOffsetProperty);
            UpdateShaderValue(GreenOffsetProperty);
            UpdateShaderValue(BlueOffsetProperty);
            UpdateShaderValue(BrightnessProperty);
            UpdateShaderValue(ContrastProperty);
            UpdateShaderValue(GammaProperty);
            UpdateShaderValue(SaturationProperty);
            UpdateShaderValue(InvertProperty);
            UpdateShaderValue(ThresholdModeProperty);
            UpdateShaderValue(ThresholdProperty);
            UpdateShaderValue(ThresholdLowProperty);
            UpdateShaderValue(ThresholdHighProperty);
            UpdateShaderValue(RangeLowProperty);
            UpdateShaderValue(RangeHighProperty);
            UpdateShaderValue(HighlightOpacityProperty);
            UpdateShaderValue(PseudoColorModeProperty);
            UpdateShaderValue(PseudoMinProperty);
            UpdateShaderValue(PseudoMaxProperty);
        }

        public Brush? Input
        {
            get => (Brush?)GetValue(InputProperty);
            set => SetValue(InputProperty, value);
        }

        public Brush? Lut
        {
            get => (Brush?)GetValue(LutProperty);
            set => SetValue(LutProperty, value);
        }

        public double ChannelMode
        {
            get => (double)GetValue(ChannelModeProperty);
            set => SetValue(ChannelModeProperty, value);
        }

        public double RedGain
        {
            get => (double)GetValue(RedGainProperty);
            set => SetValue(RedGainProperty, value);
        }

        public double GreenGain
        {
            get => (double)GetValue(GreenGainProperty);
            set => SetValue(GreenGainProperty, value);
        }

        public double BlueGain
        {
            get => (double)GetValue(BlueGainProperty);
            set => SetValue(BlueGainProperty, value);
        }

        public double RedOffset
        {
            get => (double)GetValue(RedOffsetProperty);
            set => SetValue(RedOffsetProperty, value);
        }

        public double GreenOffset
        {
            get => (double)GetValue(GreenOffsetProperty);
            set => SetValue(GreenOffsetProperty, value);
        }

        public double BlueOffset
        {
            get => (double)GetValue(BlueOffsetProperty);
            set => SetValue(BlueOffsetProperty, value);
        }

        public double Brightness
        {
            get => (double)GetValue(BrightnessProperty);
            set => SetValue(BrightnessProperty, value);
        }

        public double Contrast
        {
            get => (double)GetValue(ContrastProperty);
            set => SetValue(ContrastProperty, value);
        }

        public double Gamma
        {
            get => (double)GetValue(GammaProperty);
            set => SetValue(GammaProperty, value);
        }

        public double Saturation
        {
            get => (double)GetValue(SaturationProperty);
            set => SetValue(SaturationProperty, value);
        }

        public double Invert
        {
            get => (double)GetValue(InvertProperty);
            set => SetValue(InvertProperty, value);
        }

        public double ThresholdMode
        {
            get => (double)GetValue(ThresholdModeProperty);
            set => SetValue(ThresholdModeProperty, value);
        }

        public double Threshold
        {
            get => (double)GetValue(ThresholdProperty);
            set => SetValue(ThresholdProperty, value);
        }

        public double ThresholdLow
        {
            get => (double)GetValue(ThresholdLowProperty);
            set => SetValue(ThresholdLowProperty, value);
        }

        public double ThresholdHigh
        {
            get => (double)GetValue(ThresholdHighProperty);
            set => SetValue(ThresholdHighProperty, value);
        }

        public double RangeLow
        {
            get => (double)GetValue(RangeLowProperty);
            set => SetValue(RangeLowProperty, value);
        }

        public double RangeHigh
        {
            get => (double)GetValue(RangeHighProperty);
            set => SetValue(RangeHighProperty, value);
        }

        public double HighlightOpacity
        {
            get => (double)GetValue(HighlightOpacityProperty);
            set => SetValue(HighlightOpacityProperty, value);
        }

        public double PseudoColorMode
        {
            get => (double)GetValue(PseudoColorModeProperty);
            set => SetValue(PseudoColorModeProperty, value);
        }

        public double PseudoMin
        {
            get => (double)GetValue(PseudoMinProperty);
            set => SetValue(PseudoMinProperty, value);
        }

        public double PseudoMax
        {
            get => (double)GetValue(PseudoMaxProperty);
            set => SetValue(PseudoMaxProperty, value);
        }

        public void Apply(DisplayShaderFilterState state)
        {
            ChannelMode = (double)state.ChannelMode;
            RedGain = state.RedGain;
            GreenGain = state.GreenGain;
            BlueGain = state.BlueGain;
            RedOffset = state.RedOffset;
            GreenOffset = state.GreenOffset;
            BlueOffset = state.BlueOffset;
            Brightness = state.Brightness;
            Contrast = state.Contrast;
            Gamma = state.Gamma;
            Saturation = state.Saturation;
            Invert = state.Invert ? 1 : 0;
            ThresholdMode = (double)state.ThresholdMode;
            Threshold = state.Threshold;
            ThresholdLow = state.ThresholdLow;
            ThresholdHigh = state.ThresholdHigh;
            RangeLow = state.RangeLow;
            RangeHigh = state.RangeHigh;
            HighlightOpacity = state.HighlightOpacity;
            PseudoColorMode = (double)state.PseudoColorMode;
            PseudoMin = state.PseudoMin;
            PseudoMax = state.PseudoMax;
        }

        private static DependencyProperty RegisterShaderDouble(string name, int constantRegister, double defaultValue)
        {
            return DependencyProperty.Register(
                name,
                typeof(double),
                typeof(DisplayShaderFilterEffect),
                new UIPropertyMetadata(defaultValue, PixelShaderConstantCallback(constantRegister)));
        }

        private static ImageBrush CreateLutBrush()
        {
            BitmapImage image = new(new Uri("pack://application:,,,/ColorVision.ImageEditor;component/Assets/Colormap/colorscale_jet.jpg", UriKind.Absolute));
            image.Freeze();

            ImageBrush brush = new(image)
            {
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
                ViewboxUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewbox = new Rect(0, 0, 1, 1)
            };
            brush.Freeze();
            return brush;
        }
    }
}
