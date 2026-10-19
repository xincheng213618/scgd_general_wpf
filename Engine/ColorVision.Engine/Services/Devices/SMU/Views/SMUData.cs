using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class SMUData : ViewModelBase
    {
        /// <summary>
        /// 电压
        /// </summary>
        public double Voltage { get; set; }
        /// <summary>
        /// 电流
        /// </summary>
        public double Current { get; set; }
        internal bool HasNativeResistance { get; set; }
        internal double? NativeResistance { get; set; }
        public double? Resistance => HasNativeResistance ? NativeResistance : Local.LocalSmuDetails.ResistanceFromVI(Voltage, Current);
        public double Power => Voltage * Current / 1000;
    }
}
