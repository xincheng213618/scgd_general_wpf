#pragma warning disable CS8603
using ColorVision.UI;
using System.ComponentModel;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ProjectARVRPro.Recipe
{
    public class RecipeBasePropertiesEditor : IPropertyEditor
    {
        static RecipeBasePropertiesEditor()
        {
            PropertyEditorHelper.RegisterEditor<RecipeBasePropertiesEditor>(typeof(RecipeBase));
        }

        public DockPanel GenProperties(PropertyInfo property, object obj)
        {
             if (property.GetValue(obj) is not RecipeBase recipeBase) return null;

            var rm = PropertyEditorHelper.GetResourceManager(obj);
            var dockPanel = new DockPanel();


            var grid = new Grid() { HorizontalAlignment = HorizontalAlignment.Right, Width = 420 };
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });

            var labelStyle = new Style(typeof(TextBlock))
            {
                Setters =
                {
                    new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center),
                    new Setter(TextBlock.MarginProperty, new Thickness(3, 0, 3, 0)),
                    new Setter(TextBlock.ForegroundProperty, SystemColors.GrayTextBrush),
                    new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold),
                }
            };

            TextBlock CreateHintLabel(string text, string tooltip)
            {
                return new TextBlock() { Text = text, ToolTip = tooltip, Style = labelStyle };
            }

            var labelMin = CreateHintLabel("≥", "下限：结果需大于或等于此值");
            Grid.SetColumn(labelMin, 0);
            grid.Children.Add(labelMin);

            Binding bindingMin = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Min");
            bindingMin.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMin.StringFormat = "0.0################";
            var textboxMin = PropertyEditorHelper.CreateSmallTextBox(bindingMin);
            textboxMin.ToolTip = "下限：结果需大于或等于此值";
            textboxMin.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxMin, 1);
            grid.Children.Add(textboxMin);

            var labelMax = CreateHintLabel("≤", "上限：结果需小于或等于此值");
            Grid.SetColumn(labelMax, 2);
            grid.Children.Add(labelMax);

            Binding bindingMax = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Max");
            bindingMax.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMax.StringFormat = "0.0################";
            var textboxMax = PropertyEditorHelper.CreateSmallTextBox(bindingMax);
            textboxMax.ToolTip = "上限：结果需小于或等于此值";
            textboxMax.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxMax, 3);
            grid.Children.Add(textboxMax);

            var labelFix = CreateHintLabel("K", "修正系数 K：修正后 = 原值 * K + B");
            Grid.SetColumn(labelFix, 4);
            grid.Children.Add(labelFix);

            Binding bindingFix = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Fix");
            bindingFix.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingFix.StringFormat = "0.0################";
            var textboxFix = PropertyEditorHelper.CreateSmallTextBox(bindingFix);
            textboxFix.ToolTip = "修正系数 K：修正后 = 原值 * K + B";
            textboxFix.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxFix, 5);
            grid.Children.Add(textboxFix);

            var labelB = CreateHintLabel("B", "修正偏移 B：修正后 = 原值 * K + B");
            Grid.SetColumn(labelB, 6);
            grid.Children.Add(labelB);

            Binding bindingB = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "B");
            bindingB.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingB.StringFormat = "0.0################";
            var textboxB = PropertyEditorHelper.CreateSmallTextBox(bindingB);
            textboxB.ToolTip = "修正偏移 B：修正后 = 原值 * K + B";
            textboxB.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxB, 7);
            grid.Children.Add(textboxB);

            DockPanel.SetDock(grid, Dock.Right);
            dockPanel.Children.Add(grid);

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);


            return dockPanel;
        }
    }
}
