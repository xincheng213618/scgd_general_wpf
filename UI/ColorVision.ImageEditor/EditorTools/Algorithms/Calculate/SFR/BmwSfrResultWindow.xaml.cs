using ColorVision.Core;
using ColorVision.Themes;
using ColorVision.UI;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

public partial class BmwSfrResultWindow : Window
{
    private readonly ImageFrameLease _lease;
    private readonly DateTimeOffset _capturedAt = DateTimeOffset.Now;
    private readonly Action<IReadOnlyList<BmwTargetAnalysis>, SfrAnalysisOptions>? _updated;
    private IReadOnlyList<BmwTargetAnalysis> _results;
    private SfrAnalysisOptions _options;
    private bool _closed, _busy;
    private int _resultVersion;
    internal bool IsClosed => _closed;
    internal BmwSfrOverlaySettings DisplaySettings { get; set; } = new();
    internal BmwSfrRoiSettings MeasurementRoi { get; set; } = new();
    private double _frequency = .25;
    private string _displayChannel = "L";
    internal event Action<IReadOnlyList<BmwTargetAnalysis>, SfrAnalysisOptions, string>? DisplayUpdated;
    internal string DisplayChannel
    {
        get => _displayChannel;
        set
        {
            if (!BmwSfrPresentation.ChannelNames.Contains(value)) throw new ArgumentException("回显通道必须为 L、R、G 或 B。",nameof(value));
            if (_displayChannel == value) return;
            _displayChannel = value;
            DisplayChannelSelector.SelectedValue = value;
            RefreshResults();
            DisplayUpdated?.Invoke(_results,_options,_displayChannel);
        }
    }
    private readonly ThemeManager _themeManager = ThemeManager.Current;
    internal sealed record EdgeRow(BmwTargetAnalysis Target, BmwEdgeAnalysis Edge, string DisplayChannel)
    {
        public string TargetId => Target.Id;
        public string EdgeName => BmwSfrPresentation.EdgeName(Edge.Id);
        public string Mtf50 => BmwSfrPresentation.Number(BmwSfrPresentation.Mtf50(Edge,DisplayChannel));
        public string State => BmwSfrPresentation.Channel(Edge,DisplayChannel) is { Valid:true } channel ? channel.Mtf50.HasValue ? "可计算" : "未交叉" : "INVALID";
        public string StateDetail => BmwSfrPresentation.ChannelState(Edge,DisplayChannel);
    }
    private EdgeRow? Selected => ResultsGrid.SelectedItem as EdgeRow;
    public BmwSfrResultWindow(ImageFrameLease lease, IReadOnlyList<BmwTargetAnalysis> results, SfrAnalysisOptions options,
        Action<IReadOnlyList<BmwTargetAnalysis>, SfrAnalysisOptions>? updated = null)
    {
        _lease = lease; _results = results; _options = options with { }; _updated = updated;
        InitializeComponent();
        this.ApplyCaption();
        Width = Math.Min(Width, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(Height, SystemParameters.WorkArea.Height - 24);
        Plot.CursorReadout += text => CursorText.Text = text;
        _themeManager.CurrentUIThemeChanged += OnThemeChanged;
        Closed += (_, _) => { _closed = true; _themeManager.CurrentUIThemeChanged -= OnThemeChanged; if (!_busy) _lease.Dispose(); };
        RefreshResults();
    }
    internal void UpdateTarget(BmwTargetAnalysis target)
    {
        if(_closed || !_results.Any(t=>t.Id==target.Id && !ReferenceEquals(t,target)))return;
        _results=_results.Select(t=>t.Id==target.Id?target:t).ToArray();
        _resultVersion++;
        RefreshResults();
    }
    internal void SelectEdge(string targetId,BmwEdgeId edgeId)
    {
        if(_closed)return;
        var row=ResultsGrid.Items.OfType<EdgeRow>().FirstOrDefault(r=>r.TargetId==targetId && r.Edge.Id==edgeId);
        if(row==null)return;
        ResultsGrid.SelectedItem=row; ResultsGrid.ScrollIntoView(row);
    }
    private void RefreshResults()
    {
        string? selectedId = Selected?.TargetId;
        BmwEdgeId? selectedEdge = Selected?.Edge.Id;
        var rows = _results.SelectMany(target => target.Edges.Select(edge => new EdgeRow(target,edge,DisplayChannel))).ToArray();
        SummaryMetricColumn.Header = $"{DisplayChannel} MTF50";
        ResultsGrid.ItemsSource = rows;
        ResultsGrid.SelectedItem = rows.FirstOrDefault(r=>r.TargetId==selectedId && r.Edge.Id==selectedEdge) ?? rows.FirstOrDefault();
        SourceText.Text = $"{_lease.Image.cols} × {_lease.Image.rows} 原始像素 · {_lease.Image.depth} bit · 快照 {_capturedAt:HH:mm:ss}";
        int available = rows.Sum(r => r.Edge.Analysis?.Channels.Count(c=>c.Valid) ?? 0);
        int measured = rows.Sum(r => r.Edge.Analysis?.Channels.Count ?? 0);
        SummaryText.Text = $"{_results.Count} 个搜索框 · {_results.Count(r=>r.Located)} 个已定位 · {rows.Length} 条边 · 可计算通道 {available}/{measured} · 输入编码 {_options.InputEncoding}";
        FooterText.Text = $"测量质量检查：SNR ≥ {_options.MinimumSnr:G4}，对比度 ≥ {_options.MinimumContrast:G4}，拟合残差 ≤ {_options.MaximumFitRms:G4} px。不是产品合格判据；未知编码仅供诊断。";
        ShowSelected();
    }
    private void Edge_Changed(object sender, SelectionChangedEventArgs e) { if (IsInitialized) ShowSelected(); }
    private void DisplayChannel_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (IsInitialized && DisplayChannelSelector.SelectedValue is string channel) DisplayChannel = channel;
    }
    private void ShowSelected()
    {
        if (Selected is not { } row || Plot == null) return;
        SelectionText.Text = $"{row.TargetId} · {row.Target.ChartTypeText} · {row.EdgeName}边";
        Mtf50Text.Text = $"{DisplayChannel} MTF50  {row.Mtf50} cy/px";
        EditRoiButton.IsEnabled = !_busy && row.Target.Located && row.Edge.Roi.Width > 0;
        RenderPreview(row);
        RenderMetrics(row.Edge);
        QualityGrid.ItemsSource = BmwSfrPresentation.ChannelNames.Select(name =>
        {
            var c = BmwSfrPresentation.Channel(row.Edge,name);
            return new { Channel=name, Contrast=N(c?.PlateausAvailable==true?c.Contrast:null), Noise=N(c?.PlateausAvailable==true?c.Noise:null),
                Snr=N(c?.PlateausAvailable==true?c.Snr:null), Angle=N(c?.FitAvailable==true?c.AngleDegrees:null), FitRms=N(c?.FitAvailable==true?c.FitRms:null),
                Coverage=c?.SamplingAvailable==true?c.BinCoverage.ToString("P1"):"—", Clipped=c?.ClippedFraction.ToString("P1")??"—" };
        }).ToArray();
        SamplesGrid.ItemsSource = row.Edge.Analysis?.Channels.Where(c=>c.Valid).SelectMany(c =>
            Samples(c.Channel,"MTF",c.Frequencies,c.Mtf).Concat(Samples(c.Channel,"ESF",c.EdgePositions,c.Esf)).Concat(Samples(c.Channel,"LSF",c.LsfPositions,c.Lsf))).ToArray();
        var missing = BmwSfrPresentation.ChannelNames.Where(name => BmwSfrPresentation.Channel(row.Edge,name) is not { Valid:true });
        MissingCurvesText.Text = string.Join("\n",missing.Select(name=>$"{name} 未绘制：{BmwSfrPresentation.ChannelState(row.Edge,name)}"));
        if (MissingCurvesText.Text.Length==0) MissingCurvesText.Text="四通道均可计算；MTF50/10 未在 Nyquist 内交叉时保留空值。";
        RenderPlot();
    }
    private static IEnumerable<SampleRow> Samples(string channel,string series,double[] x,double[] values) => x.Select((value,i)=>new SampleRow(channel,series,value,values[i]));
    private sealed record SampleRow(string Channel,string Series,double X,double Value);
    private static string N(double? value) => BmwSfrPresentation.Number(value);
    private void RenderMetrics(BmwEdgeAnalysis edge)
    {
        ChannelsGrid.ItemsSource = BmwSfrPresentation.ChannelNames.Select(name =>
        {
            var c = BmwSfrPresentation.Channel(edge,name);
            return new { Channel=name, Mtf50=N(c?.Valid==true?c.Mtf50:null), Mtf10=N(c?.Valid==true?c.Mtf10:null),
                AtFrequency=c?.Valid==true?SfrCurveQueries.AtFrequency(c.Frequencies,c.Mtf,_frequency)?.ToString("P2")??"—":"—",
                State=BmwSfrPresentation.ChannelState(edge,name) };
        }).ToArray();
        FrequencyColumn.Header=$"MTF @ {_frequency:G3}";
    }
    private void RenderPreview(EdgeRow row)
    {
        PreviewCanvas.Children.Clear();
        bool edgeOnly=EdgePreview.IsChecked==true && row.Edge.Roi.Width>0;
        var roi=edgeOnly?row.Edge.Roi:row.Target.SearchRoi; var image=_lease.Image;
        bool valid=roi.X>=0&&roi.Y>=0&&roi.Width>0&&roi.Height>0&&roi.Width<=image.cols&&roi.Height<=image.rows&&roi.X<=image.cols-roi.Width&&roi.Y<=image.rows-roi.Height;
        PreviewCanvas.Width=valid?roi.Width:1; PreviewCanvas.Height=valid?roi.Height:1;
        if(valid)
        {
            PreviewCanvas.Children.Add(new Image { Source=SfrSimplePlotWindow.CreatePreview(image,roi,_options),Width=roi.Width,Height=roi.Height });
            foreach(var edge in row.Target.Edges.Where(e=>!edgeOnly && e.Roi.Width>0))
            {
                double scale=Math.Max(1,roi.Width/300.0);
                var rect=new Rectangle { Width=edge.Roi.Width,Height=edge.Roi.Height,Stroke=Brushes.Red,StrokeThickness=(edge.Id==row.Edge.Id?2:1)*scale };
                Canvas.SetLeft(rect,edge.Roi.X-roi.X); Canvas.SetTop(rect,edge.Roi.Y-roi.Y); PreviewCanvas.Children.Add(rect);
            }
        }
        if(valid && edgeOnly && BmwSfrPresentation.Channel(row.Edge,DisplayChannel) is { FitAvailable:true } channel)
        {
            double height=channel.Rotated?roi.Width:roi.Height;
            double x0=channel.EdgeIntercept,x1=x0+channel.EdgeSlope*(height-1);
            var fit=new Line { Stroke=Brushes.OrangeRed,StrokeDashArray=new DoubleCollection { 4, 3 },StrokeThickness=Math.Max(.7,Math.Max(roi.Width,roi.Height)/200.0) };
            if(channel.Rotated) { fit.X1=roi.Width-1; fit.Y1=x0; fit.X2=0; fit.Y2=x1; }
            else { fit.X1=x0; fit.Y1=0; fit.X2=x1; fit.Y2=height-1; }
            PreviewCanvas.Children.Add(fit);
        }
        var search=row.Target.SearchRoi;
        var r=row.Edge.Roi;
        RoiText.Text=$"搜索框 ({search.X}, {search.Y}, {search.Width}, {search.Height})\n{row.EdgeName}边 ({r.X}, {r.Y}, {r.Width}, {r.Height}) 原图像素\n{(row.Target.Located?edgeOnly?$"橙色为 {DisplayChannel} 边缘拟合；测量不缩放原图。":"粗红框为当前边；测量不缩放原图。":BmwSfrPresentation.Reason(row.Target.Reason))}";
    }
    private void Preview_Changed(object sender,RoutedEventArgs e) { if(Selected is { } row) RenderPreview(row); }
    private void View_Changed(object sender,RoutedEventArgs e) { if(IsInitialized) RenderPlot(); }
    private void RenderPlot()
    {
        if(Plot==null||ShowL==null) return;
        if(Selected?.Edge.Analysis is not { } analysis) { Plot.Clear(); return; }
        var visible=new HashSet<string>();
        if(ShowL.IsChecked==true)visible.Add("L"); if(ShowR.IsChecked==true)visible.Add("R"); if(ShowG.IsChecked==true)visible.Add("G"); if(ShowB.IsChecked==true)visible.Add("B");
        Plot.SetDarkTheme(_themeManager.CurrentUITheme==Theme.Dark);
        Plot.ShowResult(analysis,CurveView.SelectedIndex,visible,Extended.IsChecked==true);
    }
    private void OnThemeChanged(Theme theme)
    {
        if(_closed)return;
        if(!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(()=>OnThemeChanged(theme)); return; }
        RenderPlot();
    }
    private void Query_Click(object sender,RoutedEventArgs e)
    {
        if(!double.TryParse(FrequencyInput.Text,out double value)||!double.IsFinite(value)||value<0||value>.5) { QueryError.Text="频率范围为 0..0.5"; return; }
        _frequency=value; QueryError.Text=""; if(Selected is { } row) RenderMetrics(row.Edge);
    }
    private async void DisplaySettings_Click(object sender,RoutedEventArgs e)
    {
        if(_busy || _closed)return;
        var next=new BmwSfrViewSettings { Display=DisplaySettings.Copy(),MeasurementRoi=MeasurementRoi with { } };
        bool submitted=false;
        var dialog=new PropertyEditorWindow(next,PropertyEditorEditMode.Transactional) { Owner=this,Title="SFR 测量与显示",Width=820,Height=680,WindowStartupLocation=WindowStartupLocation.CenterOwner };
        dialog.Submitted+=(_,_)=>submitted=true; dialog.ShowDialog(); if(!submitted)return;
        try { next.Validate(); } catch(ArgumentException ex) { MessageBox.Show(this,ex.Message,"设置无效"); return; }
        DisplaySettings=next.Display;
        DisplayUpdated?.Invoke(_results,_options,DisplayChannel);
        if(next.MeasurementRoi==MeasurementRoi)return;
        _busy=true; SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=EditRoiButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=false;
        int version=_resultVersion;
        var regions=_results.Select(t=>new BmwSearchRegion(t.Id,t.SearchRoi)).ToArray();
        SummaryText.Text="正在按四边测量框设置重新定位与计算…";
        try
        {
            var results=await Task.Run(()=>BmwSfrAnalyzer.Analyze(_lease.Image,regions,_options,next.MeasurementRoi));
            if(_closed || version!=_resultVersion)return;
            _resultVersion++; _results=results; MeasurementRoi=next.MeasurementRoi;
            RefreshResults(); _updated?.Invoke(_results,_options); DisplayUpdated?.Invoke(_results,_options,DisplayChannel);
        }
        catch(Exception ex) { if(!_closed)SummaryText.Text=ex.Message; }
        finally { _busy=false; if(_closed)_lease.Dispose(); else { SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=true; EditRoiButton.IsEnabled=Selected?.Target.Located==true; } }
    }
    private async void Settings_Click(object sender,RoutedEventArgs e)
    {
        if(_busy)return;
        var next=_options with { }; bool submitted=false;
        var editor=new PropertyEditorWindow(next,PropertyEditorEditMode.Transactional) { Owner=this,Title="BMW SFR 测量质量参数" };
        editor.Submitted+=(_,_)=>submitted=true; editor.ShowDialog(); if(!submitted)return;
        try { next.Validate(); } catch(ArgumentException ex) { MessageBox.Show(this,ex.Message,"参数无效"); return; }
        _busy=true; SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=EditRoiButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=false;
        SummaryText.Text="正在重新分析固定图像快照…";
        int version=_resultVersion;
        var currentResults=_results;
        try
        {
            var results=await Task.Run(()=>Reanalyze(_lease.Image,currentResults,next));
            if(_closed || version!=_resultVersion)return;
            _resultVersion++; _results=results; _options=next; RefreshResults(); _updated?.Invoke(_results,_options); DisplayUpdated?.Invoke(_results,_options,DisplayChannel);
        }
        catch(Exception ex) { if(!_closed)SummaryText.Text=ex.Message; }
        finally { _busy=false; if(_closed)_lease.Dispose(); else { SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=true; EditRoiButton.IsEnabled=Selected?.Target.Located==true; }; }
    }
    internal sealed class EdgeRoiSettings
    {
        [DisplayName("原图 X (px)")] public int X { get; set; }
        [DisplayName("原图 Y (px)")] public int Y { get; set; }
        [DisplayName("宽度 (px)")] public int Width { get; set; }
        [DisplayName("高度 (px)")] public int Height { get; set; }
        public RoiRect ToRoi() => new(X,Y,Width,Height);
    }
    internal static bool IsInside(RoiRect roi,RoiRect parent) => roi.Width>0&&roi.Height>0&&roi.X>=parent.X&&roi.Y>=parent.Y
        && roi.Width<=parent.Width&&roi.Height<=parent.Height&&(long)roi.X+roi.Width<=(long)parent.X+parent.Width&&(long)roi.Y+roi.Height<=(long)parent.Y+parent.Height;
    internal static BmwEdgeAnalysis AnalyzeEdge(HImage image,BmwEdgeAnalysis edge,SfrAnalysisOptions options)
    {
        if (edge.SupportRoi.Width > 0 && !IsInside(edge.Roi, edge.SupportRoi))
            return edge with { Analysis = null, Valid = false, Reason = "checkerboard_roi_crosses_junction" };
        try
        {
            var analysis=SfrAnalyzer.Analyze(image,edge.Roi,options);
            string reason=string.Join(";",analysis.Channels.Where(c=>!c.Valid).Select(c=>$"{c.Channel}:{c.Reason}"));
            return edge with { Analysis=analysis,Valid=analysis.Channels.All(c=>c.Valid),Reason=reason };
        }
        catch(InvalidOperationException ex) { return edge with { Analysis=null,Valid=false,Reason=ex.Message }; }
    }
    internal static IReadOnlyList<BmwTargetAnalysis> Reanalyze(HImage image,IReadOnlyList<BmwTargetAnalysis> targets,SfrAnalysisOptions options) =>
        targets.Select(target=>target with { Edges=target.Edges.Select(edge=>target.Located&&edge.Roi.Width>0?AnalyzeEdge(image,edge,options):edge).ToArray() }).ToArray();
    private async void EditRoi_Click(object sender,RoutedEventArgs e)
    {
        if(_busy||Selected is not { } row||!row.Target.Located)return;
        var roi=row.Edge.Roi;
        var edit=new EdgeRoiSettings { X=roi.X,Y=roi.Y,Width=roi.Width,Height=roi.Height };
        var dialog=new PropertyEditorWindow(edit,PropertyEditorEditMode.Transactional) { Owner=this,Title=$"{row.TargetId} · {row.EdgeName}边 SFR 矩形" };
        bool submitted=false; dialog.Submitted+=(_,_)=>submitted=true; dialog.ShowDialog(); if(!submitted)return;
        if(!IsInside(edit.ToRoi(),row.Target.SearchRoi)) { MessageBox.Show(this,"测量框必须完整位于当前搜索外框内，宽高须大于零。","矩形范围无效"); return; }
        if(row.Edge.SupportRoi.Width>0 && !IsInside(edit.ToRoi(),row.Edge.SupportRoi)) { MessageBox.Show(this,BmwSfrPresentation.Reason("checkerboard_roi_crosses_junction"),"矩形范围无效"); return; }
        _busy=true; SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=EditRoiButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=false;
        int version=_resultVersion;
        try
        {
            var updated=await Task.Run(()=>AnalyzeEdge(_lease.Image,row.Edge with { Roi=edit.ToRoi() },_options));
            if(_closed || version!=_resultVersion)return;
            _resultVersion++;
            _results=_results.Select(t=>t.Id==row.TargetId?t with { Edges=t.Edges.Select(edge=>edge.Id==updated.Id?updated:edge).ToArray() }:t).ToArray();
            RefreshResults(); _updated?.Invoke(_results,_options); DisplayUpdated?.Invoke(_results,_options,DisplayChannel);
        }
        catch(Exception ex) { if(!_closed)SummaryText.Text=ex.Message; }
        finally { _busy=false; if(_closed)_lease.Dispose(); else { SettingsButton.IsEnabled=DisplaySettingsButton.IsEnabled=CsvButton.IsEnabled=JsonButton.IsEnabled=true; EditRoiButton.IsEnabled=Selected?.Target.Located==true; } }
    }
    private void ExportJson_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog { Filter="完整 BMW 测量 (*.json)|*.json",FileName=$"BMW_SFR_{_capturedAt:yyyyMMdd_HHmmss}.json" };
        if(dialog.ShowDialog(this)!=true)return;
        try { File.WriteAllText(dialog.FileName,JsonSerializer.Serialize(new { capturedAt=_capturedAt,sourceWidth=_lease.Image.cols,sourceHeight=_lease.Image.rows,sourceRevision=_lease.Revision,options=_options,measurementRoi=MeasurementRoi,displayChannel=DisplayChannel,targetFrequency=_frequency,results=_results },new JsonSerializerOptions { WriteIndented=true,IncludeFields=true })); }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"导出失败"); }
    }
    private void ExportCsv_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new SaveFileDialog { Filter="BMW 指标与完整曲线 (*.csv)|*.csv",FileName="BMW_SFR.csv" }; if(dialog.ShowDialog(this)!=true)return;
        try
        {
            using var writer=new StreamWriter(dialog.FileName,false,new System.Text.UTF8Encoding(true));
            writer.WriteLine("# options="+_options.ToJson());
            writer.WriteLine("Target,Edge,Channel,Valid,Reason,MTF50,MTF10,Frequency,MTF_at_frequency");
            foreach(var target in _results)foreach(var edge in target.Edges)foreach(string name in BmwSfrPresentation.ChannelNames)
            {
                var c=BmwSfrPresentation.Channel(edge,name);
                writer.WriteLine(string.Join(",",Q(target.Id),edge.Id,name,c?.Valid==true,Q(c?.Reason??edge.Reason),V(c?.Valid==true?c.Mtf50:null),V(c?.Valid==true?c.Mtf10:null),V(_frequency),V(c?.Valid==true?SfrCurveQueries.AtFrequency(c.Frequencies,c.Mtf,_frequency):null)));
            }
            writer.WriteLine("Target,Edge,Channel,Series,X,Value");
            foreach(var target in _results)foreach(var edge in target.Edges)foreach(var c in edge.Analysis?.Channels.Where(c=>c.Valid)??[])
                foreach(var sample in Samples(c.Channel,"MTF",c.Frequencies,c.Mtf).Concat(Samples(c.Channel,"ESF",c.EdgePositions,c.Esf)).Concat(Samples(c.Channel,"LSF",c.LsfPositions,c.Lsf)))
                    writer.WriteLine(string.Join(",",Q(target.Id),edge.Id,c.Channel,sample.Series,V(sample.X),V(sample.Value)));
        }
        catch(Exception ex) { MessageBox.Show(this,ex.Message,"导出失败"); }
    }
    private static string Q(string text)=>"\""+text.Replace("\"","\"\"")+"\"";
    private static string V(double? value)=>value?.ToString("G17",CultureInfo.InvariantCulture)??"";
}
