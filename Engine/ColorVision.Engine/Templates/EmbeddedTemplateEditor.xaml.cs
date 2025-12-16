using System;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Templates
{
    /// <summary>
    /// EmbeddedTemplateEditor.xaml 的交互逻辑
    /// Simplified embedded version of TemplateEditorWindow for use in TemplateManagerWindow
    /// </summary>
    public partial class EmbeddedTemplateEditor : UserControl
    {
        public ITemplate Template { get; private set; }
        public Window OwnerWindow { get; set; }

        public EmbeddedTemplateEditor()
        {
            InitializeComponent();
        }

        public void SetTemplate(ITemplate template)
        {
            Template = template;
            
            if (template != null)
            {
                // Load template data
                template.Load();
                
                // Update UI
                TemplateCountText.Text = $"模板数量: {template.Count}";
                TemplateListView.ItemsSource = template.ItemsSource;
                
                if (template.Count > 0)
                {
                    InfoText.Text = $"共 {template.Count} 个模板项，点击上方按钮打开完整编辑器";
                }
                else
                {
                    InfoText.Text = "此模板类型暂无模板项，点击上方按钮打开完整编辑器以创建";
                }
            }
            else
            {
                TemplateCountText.Text = "模板数量: 0";
                TemplateListView.ItemsSource = null;
                InfoText.Text = "未选择模板";
            }
        }

        private void OpenFullEditor_Click(object sender, RoutedEventArgs e)
        {
            if (Template != null)
            {
                var editorWindow = new TemplateEditorWindow(Template) 
                { 
                    Owner = OwnerWindow ?? Application.Current.GetActiveWindow() 
                };
                editorWindow.ShowDialog();
                
                // Refresh after editing
                SetTemplate(Template);
            }
        }
    }
}
