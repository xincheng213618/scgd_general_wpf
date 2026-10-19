using ColorVision.Common.MVVM;

namespace ColorVision.Engine.Services.Devices.SMU.Views
{
    public class SMUData : ViewModelBase
    {
        public int Index { get; internal set; }
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
        public string ResistanceDescription => HasNativeResistance ? "仪器返回的电阻测量值。" : "按本次实测电压 / 电流计算的工作点电阻，不是微分电阻。";
        public double Power => Voltage * Current / 1000;
    }
}
