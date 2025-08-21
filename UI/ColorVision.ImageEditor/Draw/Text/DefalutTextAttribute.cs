#pragma warning disable CA1711,CA2211
using ColorVision.UI;
using Newtonsoft.Json;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw
{
    public class DefalutTextAttribute : BaseMode,IConfig 
    {
        public static DefalutTextAttribute Defalut => ConfigService.Instance.GetRequiredService<DefalutTextAttribute>();

        public double ActualLength { get => _ActualLength; set { _ActualLength = value; NotifyPropertyChanged(); } }
        private double _ActualLength = 1;
        public string PhysicalUnit { get => _PhysicalUnit; set { _PhysicalUnit = value; NotifyPropertyChanged(); } }
        private string _PhysicalUnit = "Px";
        
        public bool IsUsePhysicalUnit { get => _IsUsePhysicalUnit; set { _IsUsePhysicalUnit = value; NotifyPropertyChanged(); } }
        private bool _IsUsePhysicalUnit;

        public string Text { get => _Text; set { _Text = value; NotifyPropertyChanged(); } }
        private string _Text;
        public double FontSize { get => _FontSize; set { _FontSize = value; NotifyPropertyChanged(); } }
        private double _FontSize = 10;

        [JsonIgnore]
        public Brush Brush { get => _Brush; set { _Brush = value; NotifyPropertyChanged(); } }
        private Brush _Brush = Brushes.SaddleBrown;
        [JsonIgnore]
        public FontFamily FontFamily { get => _FontFamily; set { _FontFamily = value; NotifyPropertyChanged(); } }
        private FontFamily _FontFamily = new FontFamily("Arial");

        [JsonIgnore]
        public FontStyle FontStyle { get => _FontStyle; set { _FontStyle = value; NotifyPropertyChanged(); } }
        private FontStyle _FontStyle = FontStyles.Normal;
        [JsonIgnore]
        public FontWeight FontWeight { get => _FontWeight; set { _FontWeight = value; NotifyPropertyChanged(); } }
        private FontWeight _FontWeight = FontWeights.Normal;
        [JsonIgnore]
        public FontStretch FontStretch { get => _FontStretch; set { _FontStretch = value; NotifyPropertyChanged(); } }
        private FontStretch _FontStretch = FontStretches.Normal;
        [JsonIgnore]
        public FlowDirection FlowDirection { get => _FlowDirection; set { _FlowDirection = value; NotifyPropertyChanged(); } }
        private FlowDirection _FlowDirection = FlowDirection.LeftToRight;

    }



}
