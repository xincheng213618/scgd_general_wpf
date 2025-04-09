using System.Windows.Controls;

namespace ColorVision.Engine.Interfaces
{
    public interface IDisplayAlgorithm
    {
        public int Order { get; set; }
        public string Name { get; set; }
        public UserControl GetUserControl();
    }
}
