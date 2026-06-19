#pragma warning disable CS8604
using System;
using System.Collections.Generic;
using System.Linq;
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
        private const string AllGroupName = "全部";
        private const string SystemGroupName = "系统默认";
        private const string PreparedGroupName = "当前副本";

        bool IsImport;
        public ITemplate ITemplate { get; set; }
        private readonly List<TemplateCreateSource> TemplateSources = new List<TemplateCreateSource>();
        private TemplateCreateSource? SelectedTemplateSource;
        private TemplateCreateSource? AppliedTemplateSource;
        private bool IsRefreshingGroups;

        private enum TemplateCreateSourceKind
        {
            Default,
            Prepared,
            Sample
        }

        private sealed class TemplateCreateSource
        {
            public TemplateCreateSourceKind Kind { get; set; }
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string GroupName { get; set; } = string.Empty;
            public TemplateSampleRecord? Sample { get; set; }

            public string Icon => Kind switch
            {
                TemplateCreateSourceKind.Default => "\uE8A5",
                TemplateCreateSourceKind.Prepared => "\uE8EF",
                _ => "\uE8D7"
            };

            public string SourceLabel => Kind switch
            {
                TemplateCreateSourceKind.Default => SystemGroupName,
                TemplateCreateSourceKind.Prepared => PreparedGroupName,
                _ => GroupName
            };
        }

        public TemplateCreate(ITemplate template,bool isImport =false)  
        {
            ITemplate = template;
            IsImport = isImport;
            InitializeComponent();
 
        }
        private RadioButton CreateTemplateCard(TemplateCreateSource source, bool isChecked)
        {
            RadioButton radioButton = new RadioButton()
            {
                Margin = new Thickness(3),
                GroupName = "TemplateCreateSource",
                Tag = source
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
                Width = 170,
                MinHeight = 112,
                Background = (System.Windows.Media.Brush)Application.Current.Resources["RegionBrush"],
                BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["BorderBrush"]
            };

            // Create content stack
            StackPanel contentStack = new StackPanel();

            // Template icon
            TextBlock iconBlock = new TextBlock()
            {
                Text = source.Icon,
                FontFamily = new System.Windows.Media.FontFamily("Segoe MDL2 Assets"),
                FontSize = 22,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryBrush"],
                Margin = new Thickness(0, 0, 0, 5)
            };

            TextBlock sourceBlock = new TextBlock()
            {
                Text = source.SourceLabel,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThirdlyTextBrush"],
                Margin = new Thickness(0, 0, 0, 4)
            };

            // Template title
            TextBlock titleBlock = new TextBlock()
            {
                Text = source.Title,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["GlobalTextBrush"]
            };

            // Template description
            TextBlock descBlock = new TextBlock()
            {
                Text = source.Description,
                FontSize = 10,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (System.Windows.Media.Brush)Application.Current.Resources["ThirdlyTextBrush"],
                Margin = new Thickness(0, 3, 0, 0)
            };

            contentStack.Children.Add(iconBlock);
            contentStack.Children.Add(sourceBlock);
            contentStack.Children.Add(titleBlock);
            contentStack.Children.Add(descBlock);

            card.Child = contentStack;
            radioButton.Content = card;

            // Add visual feedback for selection
            radioButton.Checked += (s, e) =>
            {
                card.BorderBrush = (System.Windows.Media.Brush)Application.Current.Resources["PrimaryBrush"];
                card.BorderThickness = new Thickness(2);
                ApplyTemplateSource(source, true);
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
                radioButton.IsChecked = true;
            }

            return radioButton;
        }

        private void BuildTemplateSources()
        {
            TemplateSources.Clear();
            if (ITemplate.HasCreateTemplateSource)
            {
                TemplateSources.Add(new TemplateCreateSource
                {
                    Kind = TemplateCreateSourceKind.Prepared,
                    Title = string.IsNullOrWhiteSpace(ITemplate.ImportName) ? PreparedGroupName : ITemplate.ImportName,
                    Description = "使用刚复制的模板内容创建",
                    GroupName = PreparedGroupName
                });
            }

            TemplateSources.Add(new TemplateCreateSource
            {
                Kind = TemplateCreateSourceKind.Default,
                Title = ColorVision.Engine.Properties.Resources.DefaultTemplate,
                Description = ColorVision.Engine.Properties.Resources.UseSystemDefaultTemplate,
                GroupName = SystemGroupName
            });

            foreach (TemplateSampleRecord sample in TemplateSampleLibrary.GetInstance().GetSamples(ITemplate))
            {
                TemplateSources.Add(new TemplateCreateSource
                {
                    Kind = TemplateCreateSourceKind.Sample,
                    Title = sample.Name,
                    Description = string.IsNullOrWhiteSpace(sample.Description) ? $"SQLite样例: {sample.UpdatedAt:yyyy-MM-dd HH:mm}" : sample.Description,
                    GroupName = sample.GroupName,
                    Sample = sample
                });
            }
        }

        private void ConfigureSourceGroups()
        {
            IsRefreshingGroups = true;
            List<string> groups = new List<string> { AllGroupName };
            groups.AddRange(TemplateSources
                .Select(it => it.GroupName)
                .Where(it => !string.IsNullOrWhiteSpace(it))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(it => it == PreparedGroupName ? 0 : it == SystemGroupName ? 1 : 2)
                .ThenBy(it => it));

            SourceGroupComboBox.ItemsSource = groups;
            SourceGroupComboBox.SelectedIndex = 0;
            IsRefreshingGroups = false;
        }

        private void RenderTemplateSourceCards()
        {
            TemplateStackPanels.Children.Clear();

            string selectedGroup = SourceGroupComboBox.SelectedItem?.ToString() ?? AllGroupName;
            List<TemplateCreateSource> visibleSources = TemplateSources
                .Where(source => selectedGroup == AllGroupName || source.GroupName.Equals(selectedGroup, StringComparison.OrdinalIgnoreCase))
                .ToList();

            TemplateSourceCountText.Text = $"{visibleSources.Count}/{TemplateSources.Count}";
            TemplateCreateSource? sourceToSelect = visibleSources.Contains(SelectedTemplateSource)
                ? SelectedTemplateSource
                : visibleSources.FirstOrDefault(source => source.Kind == TemplateCreateSourceKind.Prepared) ?? visibleSources.FirstOrDefault();

            foreach (TemplateCreateSource source in visibleSources)
            {
                TemplateStackPanels.Children.Add(CreateTemplateCard(source, source == sourceToSelect));
            }

            if (sourceToSelect == null)
            {
                ITemplate.ClearCreateTemplateSource();
                AppliedTemplateSource = null;
                UpdateCreatePreview();
            }
        }

        private bool ApplyTemplateSource(TemplateCreateSource source, bool refreshPreview)
        {
            SelectedTemplateSource = source;

            if (source.Kind != TemplateCreateSourceKind.Prepared)
                ITemplate.ClearCreateTemplateSource();

            bool isApplied = true;
            if (source.Kind == TemplateCreateSourceKind.Sample && source.Sample != null)
            {
                isApplied = ITemplate.ImportJsonContent(source.Sample.Name, source.Sample.Content);
            }

            if (!isApplied)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "模板来源加载失败", "ColorVision");
                return false;
            }

            AppliedTemplateSource = source;
            if (refreshPreview)
                UpdateCreatePreview();
            return true;
        }

        private void UpdateCreatePreview()
        {
            if (ITemplate.IsSideHide)
            {
                GridProperty.Children.Clear();
                GridProperty.Margin = new Thickness(0);
                PropertyColumn.Width = new GridLength(0);
                return;
            }

            PropertyColumn.Width = new GridLength(400);
            if (ITemplate.IsUserControl)
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

        private void SourceGroupComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsRefreshingGroups) return;
            RenderTemplateSourceCards();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            BuildTemplateSources();
            if (IsImport)
            {
                TemplateSourcePanel.Visibility = Visibility.Collapsed;
                this.Title = $"{Properties.Resources.Import} {ITemplate.Title} "+ ColorVision.Engine.Properties.Resources.Template;

            }
            else
            {
                ConfigureSourceGroups();
                RenderTemplateSourceCards();
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
            UpdateCreatePreview();
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

            if (!IsImport && SelectedTemplateSource != null && AppliedTemplateSource != SelectedTemplateSource)
            {
                if (!ApplyTemplateSource(SelectedTemplateSource, false))
                    return;
            }

            ITemplate.Create(CreateName);
            ITemplate.ClearCreateTemplateSource();
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
