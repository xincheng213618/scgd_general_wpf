using Conoscope.Core;
using System;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopePreprocessSettingsControl : UserControl
    {
        public ConoscopePreprocessSettingsControl(ConoscopeConfig config)
        {
            InitializeComponent();
            ArgumentNullException.ThrowIfNull(config);
            DataContext = config;
        }
    }
}
