using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Engine.Services.PhyCameras
{
    /// <summary>
    /// Physical camera details and management actions.
    /// </summary>
    public partial class InfoPhyCamera : UserControl
    {
        public PhyCamera Device { get; set; }
        public InfoPhyCamera(PhyCamera deviceCamera)
        {
            Device = deviceCamera;
            InitializeComponent();
            DataContext = Device;
        }

        private void ActionGrid_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Keep translated captions in equal cells; narrow detail panes use two columns.
            if (sender is UniformGrid grid)
                grid.Columns = e.NewSize.Width >= 520 ? 4 : 2;
        }
    }
}
