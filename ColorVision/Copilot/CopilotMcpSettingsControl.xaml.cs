using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Copilot
{
    public partial class CopilotMcpSettingsControl : UserControl
    {
        public CopilotMcpSettingsControl()
            : this(new CopilotSettingsViewModel())
        {
        }

        public CopilotMcpSettingsControl(CopilotSettingsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            DataContext = viewModel;
        }

        public bool HasAppliedChanges => ViewModel.HasAppliedChanges;

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
        }
    }

    public sealed class CopilotMcpSettingsProvider : IConfigSettingProvider
    {
        public IEnumerable<ConfigSettingMetadata> GetConfigSettings()
        {
            return new[]
            {
                new ConfigSettingMetadata
                {
                    Name = "MCP 服务器",
                    Description = "管理 ColorVision 本地 MCP 服务器配置。",
                    Order = 60,
                    Type = ConfigSettingType.TabItem,
                    ViewType = typeof(CopilotMcpSettingsControl)
                }
            };
        }
    }
}
