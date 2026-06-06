using System;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Filters
{
    public partial class DisplayShaderFilterWindow : Window
    {
        private readonly DisplayShaderFilterState _state;

        public DisplayShaderFilterWindow(DisplayShaderFilterState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            InitializeComponent();
            DataContext = _state;
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _state.Reset();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
