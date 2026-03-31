using ColorVision.Common.MVVM;
using ColorVision.Solution.V;
using ColorVision.UI;
using ColorVision.UI.Menus;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
    }
}
