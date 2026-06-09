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
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
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
                }
            };


            Binding bindingMin = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Min");
            bindingMin.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMin.StringFormat = "0.0################";
            var textboxMin = PropertyEditorHelper.CreateSmallTextBox(bindingMin);
            textboxMin.ToolTip = "最小值";
            textboxMin.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxMin, 0);
            grid.Children.Add(textboxMin);

            Binding bindingMax = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Max");
            bindingMax.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMax.StringFormat = "0.0################";
            var textboxMax = PropertyEditorHelper.CreateSmallTextBox(bindingMax);
            textboxMax.ToolTip = "最大值";
            textboxMax.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxMax, 1);
            grid.Children.Add(textboxMax);

            var labelFix = new TextBlock() { Text = "K", Style = labelStyle};
            Grid.SetColumn(labelFix, 2);
            grid.Children.Add(labelFix);

            Binding bindingFix = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Fix");
            bindingFix.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingFix.StringFormat = "0.0################";
            var textboxFix = PropertyEditorHelper.CreateSmallTextBox(bindingFix);
            textboxFix.ToolTip = "系数 K";
            textboxFix.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxFix, 3);
            grid.Children.Add(textboxFix);

            var labelB = new TextBlock() { Text = "B", Style = labelStyle };
            Grid.SetColumn(labelB, 4);
            grid.Children.Add(labelB);

            Binding bindingB = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "B");
            bindingB.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingB.StringFormat = "0.0################";
            var textboxB = PropertyEditorHelper.CreateSmallTextBox(bindingB);
            textboxB.ToolTip = "偏移量 B";
            textboxB.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            Grid.SetColumn(textboxB, 5);
            grid.Children.Add(textboxB);

            DockPanel.SetDock(grid, Dock.Right);
            dockPanel.Children.Add(grid);

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);


            return dockPanel;
        }
    }
}
