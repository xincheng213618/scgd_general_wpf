using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public interface IDisplayAlgorithm
    {
        public string Name { get; set; }
        public UserControl GetUserControl();
    }
}
