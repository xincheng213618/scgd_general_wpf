using System.Windows.Controls;

namespace ColorVision.Engine.Services.Devices.Algorithm
{
    public interface IAlgorithm
    {
        public string Name { get; set; }
        public UserControl GetUserControl();
    }
}
