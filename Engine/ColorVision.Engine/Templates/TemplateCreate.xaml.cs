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
        bool IsImport;
        public ITemplate ITemplate { get; set; }

        public TemplateCreate(ITemplate template,bool isImport =false)  
        {
            ITemplate = template;
            IsImport = isImport;
            InitializeComponent();
 
        }
        private string TemplateFile { get; set;  }

        private RadioButton CreateTemplateCard(string title, string description, bool isChecked)
        {
            RadioButton radioButton = new RadioButton()
            {
                IsChecked = isChecked,
                Margin = new Thickness(3)
            };

            // Try to find and apply the RadioButtonBaseStyle if it exists
            try
            {
                if (Application.Current.TryFindResource("RadioButtonBaseStyle") is Style style)
                {
                    radioButton.Style = style;
                }
            }
            catch
            {
                // Ignore if style not found
            }

            // Create a border for the card
            Border card = new Border()
            {
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(10, 8, 10, 8),
                MinWidth = 120,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["RegionBrush"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"]
            };

            // Create content stack
            StackPanel contentStack = new StackPanel();

            // Template icon
            TextBlock iconBlock = new TextBlock()
            {
                Text = "\uE8A5", // Document icon from Segoe MDL2 Assets
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 24,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryBrush"],
                Margin = new Thickness(0, 0, 0, 5)
            };

            // Template title
            TextBlock titleBlock = new TextBlock()
            {
                Text = title,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            // Template description
            TextBlock descBlock = new TextBlock()
            {
                Text = description,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThirdlyTextBrush"],
                Margin = new Thickness(0, 3, 0, 0)
            };

            contentStack.Children.Add(iconBlock);
            contentStack.Children.Add(titleBlock);
            contentStack.Children.Add(descBlock);

            card.Child = contentStack;
            radioButton.Content = card;

            // Add visual feedback for selection
            radioButton.Checked += (s, e) =>
            {
                card.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryBrush"];
                card.BorderThickness = new Thickness(2);
            };

            radioButton.Unchecked += (s, e) =>
            {
                card.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"];
                card.BorderThickness = new Thickness(1);
            };

            // Set initial state
            if (isChecked)
            {
                card.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryBrush"];
                card.BorderThickness = new Thickness(2);
            }

            return radioButton;
        }

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

            // Create default template card
            var defaultTemplateCard = CreateTemplateCard(ColorVision.Engine.Properties.Resources.DefaultTemplate,ColorVision.Engine.Properties.Resources.UseSystemDefaultTemplate, true);
            defaultTemplateCard.Checked += (s, e) => TemplateFile = string.Empty;
            TemplateStackPanels.Children.Add(defaultTemplateCard);

            // Create cards for each template file
            foreach (var item in Directory.GetFiles(TemplateFolder))
            {
                string fileName = Path.GetFileNameWithoutExtension(item);
                FileInfo fileInfo = new FileInfo(item);
                string fileDescription = $"CreateTime: {fileInfo.CreationTime:yyyy-MM-dd}";
                
                var templateCard = CreateTemplateCard(fileName, fileDescription, false);
                templateCard.Checked += (s, e) => TemplateFile = Path.GetFullPath(item);
                TemplateStackPanels.Children.Add(templateCard);
            }
            if (IsImport)
            {
                TemplateStackPanels.Visibility = Visibility.Collapsed;
                this.Title = $"{Properties.Resources.Import} {ITemplate.Title} "+ ColorVision.Engine.Properties.Resources.Template;

            }
            else
            {
                this.Title += ITemplate.Title + " " + ColorVision.Engine.Properties.Resources.Template;
            }
            List<string> list =
            [
                ITemplate.NewCreateFileName(ITemplate.Code),
                ITemplate.NewCreateFileName(ITemplate.Code + "_" + TemplateSetting.Instance.DefaultCreateTemplateName),
                ITemplate.NewCreateFileName(TemplateSetting.Instance.DefaultCreateTemplateName),
            ];
            if (!string.IsNullOrWhiteSpace(ITemplate.ImportName))
                list.Insert(0, ITemplate.ImportName);

            CreateCode.ItemsSource = list;
            CreateCode.SelectedIndex = 0;
            if (ITemplate.IsSideHide)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
            }
            else if (ITemplate.IsUserControl)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(5, 5, 5, 5);
                UserControl userControl = ITemplate.CreateUserControl();
                userControl.Height = double.NaN;
                userControl.Width = double.NaN;

                if (userControl is ITemplateUserControl templateUserControl)
                {
                    GridProperty.Children.Add(userControl);
                    templateUserControl.SetParam(ITemplate.CreateDefault());
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
                MessageBox.Show(Application.Current.GetActiveWindow(),ColorVision.Engine.Properties.Resources.InputTemplateName, "ColorVision");
                return;
            }
            if (ITemplate.ExitsTemplateName(CreateName))
            {
                var template = TemplateControl.FindDuplicateTemplate(CreateName);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"{template?.GetType()?.Name} "+ColorVision.Engine.Properties.Resources.AlreadyExists+" {CreateName}"+ColorVision.Engine.Properties.Resources.Template, "Template Manager");
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
