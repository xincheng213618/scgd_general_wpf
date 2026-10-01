using LocalizedText = global::ColorVision.ImageEditor.DisplayText;
using ColorVision.ImageEditor.Algorithms.Mtf;
using ColorVision.Themes;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Windows;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.Mtf;

public partial class StripeMtfResultWindow : Window
{
    public StripeMtfResultWindow(JObject result, StripeMtfParameters parameters, double elapsedMs)
    {
        InitializeComponent(); this.ApplyCaption();
        Summary.Text = LocalizedText.Format($"{result["result"]!.Count()} 个测量框 · {(parameters.PercentageDisplay ? LocalizedText.Get("百分数 (%)") : LocalizedText.Get("比例值"))} · 计算 {elapsedMs:F2} ms");
        Measurements.ItemsSource = BuildRows(result);
        Groups.ItemsSource = (result["resultChild"] as JArray ?? []).Select(g => new
        { Name = g.Value<string>("name"), Horizontal = g.Value<double>("horizontalAverage"), Vertical = g.Value<double>("verticalAverage"), Average = g.Value<double>("Average") }).ToArray();
        GroupsTab.Visibility = parameters.IsFourPart ? Visibility.Visible : Visibility.Collapsed;
        JsonText.Text = result.ToString();
    }

    internal static StripeMtfRow[] BuildRows(JObject result) => result["result"]!.Select(r => new StripeMtfRow(
        r.Value<string>("name")!, r.Value<int?>("id"), r.Value<double>("mtfValue"), r.Value<int>("x"), r.Value<int>("y"), r.Value<int>("w"), r.Value<int>("h"))).ToArray();

    private void CopyJson(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(JsonText.Text); }
        catch (Exception error) { MessageBox.Show(this, error.Message, LocalizedText.Get("复制失败")); }
    }
}

internal sealed record StripeMtfRow(string Name, int? Id, double Value, int X, int Y, int Width, int Height);
