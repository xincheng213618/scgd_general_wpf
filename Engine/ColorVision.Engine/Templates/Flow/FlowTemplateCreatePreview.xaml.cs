using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Templates.Flow;

public partial class FlowTemplateCreatePreview : UserControl, ITemplateUserControl
{
    public FlowTemplateCreatePreview() => InitializeComponent();

    public void SetParam(object param)
    {
        if (param is not FlowParam flow) throw new ArgumentException(EngineLocalization.Get("需要流程模板。"), nameof(param));
        CanvasImage.Source = null;
        SetupItems.ItemsSource = flow.CreationPreset?.Setup.Select(EngineLocalization.Get).ToArray();
        SetupTitle.Visibility = flow.CreationPreset != null ? Visibility.Visible : Visibility.Collapsed;
        EmptyHint.Visibility = string.IsNullOrEmpty(flow.DataBase64) ? Visibility.Visible : Visibility.Collapsed;
        if (string.IsNullOrEmpty(flow.DataBase64)) return;
        // Render directly without touching the user's flow/cover databases.
        using var stream = new MemoryStream(FlowTemplateCoverRenderer.Render(Convert.FromBase64String(flow.DataBase64), FlowTemplateCoverStyle.Current));
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();
        CanvasImage.Source = bitmap;
    }
}
