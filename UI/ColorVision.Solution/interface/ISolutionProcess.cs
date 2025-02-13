using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Solution
{
    public interface ISolutionProcess
    {
        string Name { get; set; }
        public string GuidId { get; }

        Control UserControl { get; }

        ImageSource IconSource { get; }

        void Open();
        void Close();
    }
}
