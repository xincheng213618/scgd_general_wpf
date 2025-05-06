using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Engine.Templates
{

    public interface ITemplateUserControl
    {
        public void SetParam(object param);
    }


    public partial class TemplateCreate : Window
    {
        public ITemplate ITemplate { get; set; }

        public TemplateCreate(ITemplate template,bool IsImport =false)  
        {
            ITemplate = template;
            InitializeComponent();
        }
        private string TemplateFile { get; set;  }
        private void Window_Initialized(object sender, EventArgs e)
        {
            string AssemblyCompanyFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), Environments.AssemblyCompany);
            if (!Directory.Exists(AssemblyCompanyFolder))
                Directory.CreateDirectory(AssemblyCompanyFolder);

            string TemplateFolders = Path.Combine(AssemblyCompanyFolder, "Templates");
            if (!Directory.Exists(TemplateFolders))
                Directory.CreateDirectory(TemplateFolders);
            string TemplateFolder = Path.Combine(TemplateFolders, ITemplate.Code);
            if (!Directory.Exists(TemplateFolder))
                Directory.CreateDirectory(TemplateFolder);

            RadioButton radioButton = new RadioButton() { Content = "默认模板", IsChecked = true, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3) };
            radioButton.Checked += (s, e) => TemplateFile = string.Empty;
            TemplateStackPanels.Children.Add(radioButton);

            foreach (var item in Directory.GetFiles(TemplateFolder))
            {
                RadioButton radioButton1 = new RadioButton() { Content = Path.GetFileNameWithoutExtension(item) ,HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(3) };
                radioButton1.Checked += (s, e) => TemplateFile = Path.GetFullPath(item);
                TemplateStackPanels.Children.Add(radioButton1);
            }

            this.Title += ITemplate.Title + " "+"模板";
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(TemplateSetting.Instance.DefaultCreateTemplateName),
            ];

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
            if (ITemplate.IsSideHide)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                this.Height = 250;

            }
            else if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                this.Height = 250;
                UserControl userControl = ITemplate.CreateUserControl();
                if (userControl is ITemplateUserControl templateUserControl)
                {
                    GridProperty.Children.Add(userControl);
                    templateUserControl.SetParam(ITemplate.CreateDefault());
                    this.Height = userControl.Height +250;
                    this.Width = userControl.Width + 40;
                }
            }
            else
            {
                PropertyGrid1.SelectedObject = ITemplate.CreateDefault();
            }
        }


        public string CreateName { get; set; }
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            CreateName = CreateCode.Text;
            if (string.IsNullOrEmpty(CreateName))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(),"请输入模板名称", "ColorVision");
                return;
            }
            if (ITemplate.ExitsTemplateName(CreateName))
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), $"已经存在{CreateName}模板", "Template Manager");
                return;
            }

            if (File.Exists(TemplateFile))
            {
                ITemplate.ImportFile(TemplateFile);
            }

            ITemplate.Create(CreateName);
            this.Close();
        }

        private void TextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                Common.NativeMethods.Keyboard.PressKey(0x09);
                e.Handled = true;
            }
        }
    }
}
