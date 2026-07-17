#pragma warning disable CA1859
using ColorVision.UI;
using ColorVision.Solution.Explorer;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Solution
{
    public class SolutionStatusBarProvider : IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            return new List<StatusBarMeta>
            {
                new StatusBarMeta
                {
                    Id = "SolutionName",
                    Name = "Solution",
                    Description = "Solution Explorer",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 0,
                    BindingName = nameof(SolutionManager.CurrentSolutionExplorer) + ".Name",
                    Source = SolutionManager.GetInstance(),
                    ActionType = StatusBarActionType.Popup,
                    PopupContentFactory = CreateSolutionPopup,
                },
                new StatusBarMeta
                {
                    Id = "SolutionConfiguration",
                    Name = "Solution Configuration",
                    Description = "当前解决方案配置",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 100,
                    BindingName = nameof(SolutionManager.CurrentSolutionExplorer) + "." + nameof(SolutionExplorer.ActiveConfiguration),
                    Source = SolutionManager.GetInstance(),
                    ActionType = StatusBarActionType.Popup,
                    PopupContentFactory = CreateConfigurationPopup,
                }
            };
        }

        private static FrameworkElement CreateSolutionPopup()
        {
            var manager = SolutionManager.GetInstance();
            var stack = new StackPanel { MinWidth = 200 };

            if (manager.CurrentSolutionExplorer != null)
            {
                var nameBlock = new TextBlock
                {
                    Text = manager.CurrentSolutionExplorer.Name,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(8, 4, 8, 4),
                };
                nameBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stack.Children.Add(nameBlock);

                stack.Children.Add(new Separator());

                var openFolderItem = new Button
                {
                    Content = "Open in Explorer",
                    Margin = new Thickness(4),
                    Padding = new Thickness(8, 4, 8, 4),
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                };
                openFolderItem.SetResourceReference(Control.BackgroundProperty, "GlobalBackground");
                openFolderItem.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
                openFolderItem.Click += (s, e) =>
                {
                    if (manager.CurrentSolutionExplorer?.DirectoryInfo?.Exists == true)
                        Process.Start("explorer.exe", manager.CurrentSolutionExplorer.DirectoryInfo.FullName);
                };
                stack.Children.Add(openFolderItem);

                var pathBlock = new TextBlock
                {
                    Text = manager.CurrentSolutionExplorer.DirectoryInfo?.FullName ?? "",
                    FontSize = 11,
                    Opacity = 0.7,
                    Margin = new Thickness(8, 2, 8, 4),
                    TextWrapping = TextWrapping.Wrap,
                    MaxWidth = 300,
                };
                pathBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stack.Children.Add(pathBlock);
            }
            else
            {
                var noSolution = new TextBlock
                {
                    Text = "No solution opened",
                    Margin = new Thickness(8, 4, 8, 4),
                };
                noSolution.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stack.Children.Add(noSolution);
            }

            return stack;
        }

        private static FrameworkElement CreateConfigurationPopup()
        {
            SolutionExplorer? explorer = SolutionManager.GetInstance().CurrentSolutionExplorer;
            var stack = new StackPanel { MinWidth = 220 };
            var title = new TextBlock
            {
                Text = "活动解决方案配置",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(8, 6, 8, 4),
            };
            title.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            stack.Children.Add(title);
            stack.Children.Add(new Separator());

            if (explorer == null)
            {
                var empty = new TextBlock
                {
                    Text = "未打开解决方案",
                    Margin = new Thickness(8, 6, 8, 6),
                };
                empty.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
                stack.Children.Add(empty);
                return stack;
            }

            foreach (string configuration in explorer.GetAvailableSolutionConfigurations())
            {
                bool isSelected = string.Equals(
                    explorer.ActiveConfiguration,
                    configuration,
                    StringComparison.OrdinalIgnoreCase);
                var button = CreatePopupButton($"{(isSelected ? "●" : "  ")}  {configuration}");
                button.IsEnabled = !isSelected;
                button.Click += (_, _) => explorer.SetActiveConfiguration(configuration);
                stack.Children.Add(button);
            }

            stack.Children.Add(new Separator { Margin = new Thickness(0, 4, 0, 4) });
            var managerButton = CreatePopupButton("配置管理器...");
            managerButton.Command = SolutionProjectCommands.ConfigurationManager;
            managerButton.CommandTarget = Application.Current?.MainWindow;
            stack.Children.Add(managerButton);
            return stack;
        }

        private static Button CreatePopupButton(string content)
        {
            var button = new Button
            {
                Content = content,
                Margin = new Thickness(4, 1, 4, 1),
                Padding = new Thickness(8, 5, 8, 5),
                HorizontalContentAlignment = HorizontalAlignment.Left,
            };
            button.SetResourceReference(Control.BackgroundProperty, "GlobalBackground");
            button.SetResourceReference(Control.ForegroundProperty, "GlobalTextBrush");
            return button;
        }
    }
}
