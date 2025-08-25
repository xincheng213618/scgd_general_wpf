using ColorVision.Engine.Templates.Flow;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Extension;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ColorVision.Database;

namespace ColorVision.Engine.Templates.Jsons.LargeFlow
{

    public interface IRecipe
    {

    }


    public class RecipeManager
    {
        private static RecipeManager _instance;
        private static readonly object _locker = new();
        public static RecipeManager GetInstance() { lock (_locker) { _instance ??= new RecipeManager(); return _instance; } }

        public List<Type> RecipeTypes { get; set; }
        public RecipeManager()
        {
            RecipeTypes = new List<Type>();
            foreach (var assembly in AssemblyHandler.GetInstance().GetAssemblies())
            {
                foreach (Type type in assembly.GetTypes().Where(t => typeof(IRecipe).IsAssignableFrom(t) && !t.IsAbstract))
                {
                    RecipeTypes.Add(type);
                }
            }

        }

    }



    /// <summary>
    /// EditLargeFlow.xaml 的交互逻辑
    /// </summary>
    public partial class EditLargeFlow : Window
    {
        public TJLargeFlowParam TJLargeFlowParam { get; set; }

        public LargeFlowConfig LargeFlowConfig { get; set; }

        public EditLargeFlow(TJLargeFlowParam tJLargeFlowParam)
        {
            TJLargeFlowParam = tJLargeFlowParam;
            LargeFlowConfig = tJLargeFlowParam.LargeFlowConfig;
            InitializeComponent();
            this.ApplyCaption();
        }
        public ObservableCollection<TemplateModel<FlowParam>> LargeFlowParams { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();
        public ObservableCollection<TemplateModel<FlowParam>> LargeFlowParamAll { get; set; } = new ObservableCollection<TemplateModel<FlowParam>>();

        object recipeInstance;

        private void Window_Initialized(object sender, EventArgs e)
        {
            this.DataContext = LargeFlowConfig;
            var flowNames = new HashSet<string>(LargeFlowConfig.Flows);
            var largeFlowParamsSet = new HashSet<TemplateModel<FlowParam>>();

            foreach (var param in TemplateFlow.Params)
            {
                if (flowNames.Contains(param.Value.Name))
                {
                    largeFlowParamsSet.Add(param);
                    LargeFlowParams.Add(param);
                }
                LargeFlowParamAll.Add(param);
            }

            SeriesExportTreeView1.ItemsSource = LargeFlowParams;
            SeriesExportTreeView2.ItemsSource = LargeFlowParamAll;


            var list = RecipeManager.GetInstance().RecipeTypes.Select(x => new KeyValuePair<string, Type>(x.Name, x));
            CBRecipeTypes.ItemsSource = list;
            CBRecipeTypes.SelectionChanged += (s, e) =>
            {
                if (CBRecipeTypes.SelectedItem is KeyValuePair<string, Type> recipeType)
                {
                    if (string.IsNullOrWhiteSpace(LargeFlowConfig.ReceiptConfig))
                    {
                         recipeInstance = Activator.CreateInstance(recipeType.Value);
                        textEditor.Text = recipeInstance.ToJsonN();
                    }
                    else
                    {
                        try
                        {
                            recipeInstance = JsonConvert.DeserializeObject(LargeFlowConfig.ReceiptConfig, recipeType.Value);
                            textEditor.Text = recipeInstance.ToJsonN();
                        }
                        catch
                        {
                             recipeInstance = Activator.CreateInstance(recipeType.Value);
                            textEditor.Text = recipeInstance.ToJsonN();
                        }
                    }

  
                }
            };

        }


        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> deviceService)
            {
                LargeFlowParamAll.Remove(deviceService);
                LargeFlowParamAll.Insert(LargeFlowParamAll.Count, deviceService);
                deviceService.IsSelected = true;
            }
        }

        public static async void GetFocus(TreeView treeView, int index)
        {
            await Task.Delay(1);

            TreeViewItem firstNode = treeView.ItemContainerGenerator.ContainerFromIndex(index) as TreeViewItem;

            // 选中第一个节点
            if (firstNode != null)
            {
                firstNode.IsSelected = true;
                firstNode.Focus();
            }
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                int index = LargeFlowParamAll.IndexOf(mQTTDevice);
                if (index - 1 >= 0)
                {
                    LargeFlowParamAll.Remove(mQTTDevice);
                    LargeFlowParamAll.Insert(index - 1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }

            }
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                int index = LargeFlowParamAll.IndexOf(mQTTDevice);
                if (index + 1 < LargeFlowParamAll.Count)
                {
                    LargeFlowParamAll.Remove(mQTTDevice);
                    LargeFlowParamAll.Insert(index + 1, mQTTDevice);
                    mQTTDevice.IsSelected = true;
                }


            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParamAll.Remove(mQTTDevice);
                LargeFlowParamAll.Insert(0, mQTTDevice);
                mQTTDevice.IsSelected = true;
            }
        }

        private void Grid_MouseUp(object sender, MouseButtonEventArgs e)
        {

        }

        private void Grid_MouseMove(object sender, MouseEventArgs e)
        {

        }

        private void Grid_MouseDown(object sender, MouseButtonEventArgs e)
        {

        }

        private void Button_Click_04(object sender, RoutedEventArgs e)
        {
            foreach (var item in LargeFlowParamAll)
            {
                if (!LargeFlowParams.Contains(item))
                    LargeFlowParams.Add(item);
            }
            LargeFlowParamAll.Clear();
        }

        private void Button_Click_03(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView2.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParamAll.Remove(mQTTDevice);
                LargeFlowParams.Add(mQTTDevice);
            }
        }

        private void Button_Click_02(object sender, RoutedEventArgs e)
        {
            if (SeriesExportTreeView1.SelectedItem is TemplateModel<FlowParam> mQTTDevice)
            {
                LargeFlowParams.Remove(mQTTDevice);
                LargeFlowParamAll.Add(mQTTDevice);
            }
        }

        private void Button_Click_01(object sender, RoutedEventArgs e)
        {
            foreach (var item in LargeFlowParams)
            {

                if (!LargeFlowParamAll.Contains(item))
                    LargeFlowParamAll.Add(item);

            }
            LargeFlowParams.Clear();

        }

        private void FilterButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {

        }

        private void GridViewColumnSort(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {

        }

        private void Window_Closed(object sender, EventArgs e)
        {
            LargeFlowConfig.ReceiptConfig = textEditor.Text;
            LargeFlowConfig.Flows.Clear();
            foreach (var item in LargeFlowParams)
            {
                if (!LargeFlowConfig.Flows.Contains(item.Key))
                    LargeFlowConfig.Flows.Add(item.Key);
            }
            TJLargeFlowParam.JsonValue = JsonConvert.SerializeObject(LargeFlowConfig);
            TemplateJsonDao.Instance.Save(TJLargeFlowParam.TemplateJsonModel);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
