#pragma warning disable
using ColorVision.Common.MVVM;
using ColorVision.Engine.Templates.Jsons.LargeFlow;
using ColorVision.UI;
using Newtonsoft.Json.Linq;
using NPOI.SS.Formula.Functions;
using ProjectARVRPro;
using ProjectARVRPro;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process;
using ProjectARVRPro.Process.Black;
using ProjectARVRPro.Recipe;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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



            

            UniformGrid uniformGrid = new UniformGrid() { Columns = 2 ,HorizontalAlignment = HorizontalAlignment.Right,Width =200};
            
            Binding bindingMin = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Min");
            bindingMin.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMin.StringFormat = "0.0################";
            var textboxMin = PropertyEditorHelper.CreateSmallTextBox(bindingMin);
            textboxMin.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            uniformGrid.Children.Add(textboxMin);

            Binding bindingMax = PropertyEditorHelper.CreateTwoWayBinding(recipeBase, "Max");
            bindingMax.UpdateSourceTrigger = UpdateSourceTrigger.Default;
            bindingMax.StringFormat = "0.0################";
            var textbox = PropertyEditorHelper.CreateSmallTextBox(bindingMax);
            textbox.PreviewKeyDown += PropertyEditorHelper.TextBox_PreviewKeyDown;
            uniformGrid.Children.Add(textbox);
            

            DockPanel.SetDock(uniformGrid, Dock.Right);
            dockPanel.Children.Add(uniformGrid);

            var textBlock = PropertyEditorHelper.CreateLabel(property, rm);
            dockPanel.Children.Add(textBlock);


            return dockPanel;
        }
    }
}