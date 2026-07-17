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
            var items = new List<StatusBarMeta>
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
                    Id = "SolutionOpening",
                    Name = "Workspace Opening",
                    Description = "正在打开的工作区",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Left,
                    Order = 1,
                    BindingName = nameof(SolutionManager.WorkspaceOpenStatus),
                    Source = SolutionManager.GetInstance(),
                    ActionType = StatusBarActionType.Popup,
                    PopupContentFactory = CreateOpeningPopup,
                }
            };
            if (SolutionFeatureVisibility.ShowBuildAndDebugUI)
            {
                items.Add(new StatusBarMeta
                {
                    Id = "SolutionConfiguration",
                    Name = "Solution Configuration",
                    Description = "当前解决方案配置",
                    Type = StatusBarType.Text,
                    Alignment = StatusBarAlignment.Right,
                    Order = 100,
                    BindingName = nameof(SolutionManager.CurrentSolutionExplorer) + "." + nameof(SolutionExplorer.ActiveConfigurationDisplay),
                    Source = SolutionManager.GetInstance(),
                    ActionType = StatusBarActionType.Popup,
                    PopupContentFactory = CreateConfigurationPopup,
                });
            }
            return items;
        }

        private static FrameworkElement CreateOpeningPopup()
        {
            SolutionManager manager = SolutionManager.GetInstance();
            var stack = new StackPanel { MinWidth = 260 };
            var pathBlock = new TextBlock
            {
                Text = manager.IsOpeningWorkspace
                    ? manager.OpeningWorkspacePath
                    : "当前没有正在打开的工作区",
                Margin = new Thickness(8, 6, 8, 6),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360,
            };
            pathBlock.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            stack.Children.Add(pathBlock);
            if (!manager.IsOpeningWorkspace)
                return stack;

            var cancelButton = new Button
            {
                Content = "取消打开",
                Margin = new Thickness(8, 2, 8, 8),
                Padding = new Thickness(10, 4, 10, 4),
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            cancelButton.Click += (_, _) => manager.CancelWorkspaceOpen();
            stack.Children.Add(cancelButton);
            return stack;
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
            var platformTitle = new TextBlock
            {
                Text = "活动解决方案平台",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(8, 4, 8, 4),
            };
            platformTitle.SetResourceReference(TextBlock.ForegroundProperty, "GlobalTextBrush");
            stack.Children.Add(platformTitle);
            foreach (string platform in explorer.GetAvailableSolutionPlatforms())
            {
                bool isSelected = string.Equals(
                    explorer.ActivePlatform,
                    platform,
                    StringComparison.OrdinalIgnoreCase);
                var button = CreatePopupButton($"{(isSelected ? "●" : "  ")}  {platform}");
                button.IsEnabled = !isSelected;
                button.Click += (_, _) => explorer.SetActivePlatform(platform);
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
