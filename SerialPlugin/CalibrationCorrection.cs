using ColorVision.Common.MVVM;
using ColorVision.UI;
using System.Windows;

namespace ColorVision.Tools
{
    public class CalibrationCorrection : IMenuItem
    {
        public string? OwnerGuid => "Tool";

        public string? GuidId => "CalibrationCorrection";

        public int Order => 6;

        public string? Header => "校正生成工具";

        public string? InputGestureText => null;

        public object? Icon => null;

        public RelayCommand Command => new RelayCommand(A => Execute());

        private static void Execute()
        {
            MessageBox.Show("1");
        }
    }
}
