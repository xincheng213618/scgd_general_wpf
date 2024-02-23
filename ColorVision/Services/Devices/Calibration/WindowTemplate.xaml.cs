using ColorVision.Services.Templates;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Services.Devices.Calibration
{
    /// <summary>
    /// CalibrationTemplate.xaml 的交互逻辑
    /// </summary>
    public partial class CalibrationTemplate : Window
    {
        public ObservableCollection<TemplateModelBase> TemplateModelBases { get; set; } = new ObservableCollection<TemplateModelBase>();

        public CalibrationTemplate()
        {
            InitializeComponent();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            ListView1.ItemsSource = TemplateModelBases;
            this.Closed += WindowTemplate_Closed;
        }

        private void WindowTemplate_Closed(object? sender, EventArgs e)
        {
            TemplateSave();
        }

        private void ListView1_PreviewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
            }
        }

        private void ListView1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ListView listView && listView.SelectedIndex > -1)
            {
            }
        }
        public string NewCreateFileName(string FileName)
        {
            List<string> Names = new List<string>();
            foreach (var item in TemplateModelBases)
            {
                Names.Add(item.Key);
            }
            for (int i = 1; i < 9999; i++)
            {
                if (!Names.Contains($"{FileName}{i}"))
                    return $"{FileName}{i}";
            }
            return FileName;
        }

        private void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            TemplateSave();
        }

        private void Button_New_Click(object sender, RoutedEventArgs e)
        {
            TemplateNew();
        }


        private void Button_Del_Click(object sender, RoutedEventArgs e)
        {
        }

        public void TemplateSave()
        {
            this.Close();
        }


        public void TemplateNew()
        {
            if (!string.IsNullOrEmpty(TextBox1.Text))
            {

                TextBox1.Text = NewCreateFileName("default");
            }
            else
            {
                MessageBox.Show("请输入模板名称", Application.Current.MainWindow.Title, MessageBoxButton.OK);
            }
        }

        private void CreateNewTemplate<T>(ObservableCollection<TemplateModel<T>> keyValuePairs, string Name, T t) where T : ParamBase
        {
            keyValuePairs.Add(new TemplateModel<T>(Name, t));
            TemplateModel<T> config = new TemplateModel<T> {Value = t, Key = Name, };
            TemplateModelBases.Add(config);
            ListView1.SelectedIndex = TemplateModelBases.Count - 1;
            ListView1.ScrollIntoView(config);
        }
        private void SCManipulationBoundaryFeedback(object sender, ManipulationBoundaryFeedbackEventArgs e)
        {
            e.Handled = true;
        }


        private void ListView1_Loaded(object sender, RoutedEventArgs e)
        {
            TextBox1.Text = NewCreateFileName("default");
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = false;
            }
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e)
        {

        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                if (e.Key == Key.F2)
                {
                    templateModelBase.IsEditMode = true;
                }
                if (e.Key == Key.Escape || e.Key == Key.Enter)
                {
                    templateModelBase.IsEditMode = false;
                }
            }
        }

        private void TextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is TemplateModelBase templateModelBase)
            {
                templateModelBase.IsEditMode = true;
            }
        }


        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is TemplateModelBase templateModelBase)  
            {
                templateModelBase.IsEditMode = true;
            }
        }

        private void Button_Export_Click(object sender, RoutedEventArgs e)
        {
            if (ListView1.SelectedIndex < 0)
            {
                MessageBox.Show("请选择您要导出的流程", "ColorVision", MessageBoxButton.OK, MessageBoxImage.None, MessageBoxResult.None, MessageBoxOptions.DefaultDesktopOnly);
                return;
            }

        }

        private void Button_Import_Click(object sender, RoutedEventArgs e)
        {
        }
    }
}
