using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Engine.Media;

public partial class CvcieTemplateWindow : Window
{
    public CvcieTemplateDraft Draft { get; }
    public CvcieTemplateWindow(string template)
    {
        Draft = new CvcieTemplateDraft(template);
        InitializeComponent();
        DataContext = Draft;
        this.ApplyCaption();
    }
    private void Apply_Click(object sender, RoutedEventArgs e) => DialogResult = true;
}
