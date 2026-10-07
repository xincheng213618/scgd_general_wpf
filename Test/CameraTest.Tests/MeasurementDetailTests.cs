using CameraTest.Application;
using CameraTest.Models;
using ColorVision.Core;
using ColorVision.UI.Tests;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace CameraTest.Tests;

public sealed class MeasurementDetailTests
{
    private static FrameAnalysis Recorded(TestFrame frame, SfrAnalysisResult? analysis, RoiRect roi) => new(frame.Id, frame.Source, frame.Data.CapturedAt,
        frame.Data.Width, frame.Data.Height, frame.Data.BitDepth, frame.Data.Channels, .25, new() { InputEncoding = SfrInputEncoding.Power, DecodeExponent = 2 }, 12,
        [new("Point_1",new(0,0,frame.Data.Width,frame.Data.Height),analysis != null,analysis == null ? "target_not_found" : "",default,0,0,
            [new(BmwEdgeId.Left,roi,analysis?.Channels.All(c => c.Valid) == true,analysis == null ? "target_not_found" : "",analysis)])]);

    [Fact]
    public void PreviewCopiesExact16BitRoiWithPaddedStrideAndRejectsDifferentFrames()
    {
        var frame = new TestFrame(new(new byte[] { 1,2,3,4,99,99,5,6,7,8,99,99 },2,2,16,1,6,DateTimeOffset.Now),"test");
        var measured = Recorded(frame,new(),new(1,0,1,2));
        var detail = new MeasurementDetailSnapshot(frame,measured,"Point_1","Left",.25);
        byte[] crop = new byte[4]; detail.Preview.CopyPixels(crop,2,0);
        Assert.Equal(new byte[] { 3,4,7,8 },crop);
        Assert.True(detail.Preview.IsFrozen);
        Assert.False(detail.IsSearchPreview);
        frame.Data.Pixels[2] = 255;
        detail.Preview.CopyPixels(crop,2,0);
        Assert.Equal(3,crop[0]);
        Assert.Throws<ArgumentException>(() => new MeasurementDetailSnapshot(new(frame.Data,"another"),measured,"Point_1","Left",.25));
    }

    [Fact]
    public void DetailsKeepFailedFitEvidenceAndDoNotInventChannelsOrRecalculate()
    {
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml",UriKind.Relative) });
            var frame = new TestFrame(new(new byte[80*80],80,80,8,1,80,DateTimeOffset.Now),"synthetic");
            var channel = new SfrChannelAnalysis { Channel="L", Valid=false, Reason="edge_angle_out_of_range", FitAvailable=true, EdgeSlope=.01, EdgeIntercept=30, AngleDegrees=.57, FitRms=.1 };
            var measured = Recorded(frame,new() { AlgorithmVersion="recorded-test",Channels=[channel] },new(10,10,60,60));
            var snapshot = new MeasurementDetailSnapshot(frame,measured,"Point_1","Left",.25);
            var window = new MeasurementDetailWindow(snapshot,"R");
            try
            {
                Assert.Single(((ComboBox)window.FindName("ChannelSelector")).Items);
                Assert.Equal("Y (L)",((ComboBox)window.FindName("ChannelSelector")).SelectedItem);
                Assert.Equal("—",((TextBlock)window.FindName("Mtf50Value")).Text);
                Assert.Contains("0.57",((TextBlock)window.FindName("DiagnosticText")).Text);
                Assert.Contains("指数 2",((TextBlock)window.FindName("ConditionsText")).Text);
                Assert.Equal(Visibility.Visible,((Line)window.FindName("FitLine")).Visibility);
                Assert.Same(measured.Targets[0].Edges[0],snapshot.Edge);
                Assert.Same(channel,snapshot.Rows[0].ChannelAnalysis);
            }
            finally { window.Close(); }
            var failed = Recorded(frame,null,default);
            var missing = new MeasurementDetailSnapshot(frame,failed,"Point_1","Left",.25);
            Assert.True(missing.IsSearchPreview);
            Assert.Equal(80,missing.Preview.PixelWidth);
            Assert.Null(missing.Edge.Analysis);
        });
    }

    [Fact]
    public void CameraResultEntryKeepsCurrentAnalysisAndRejectsStaleFrame()
    {
        WpfTestHost.Invoke(() =>
        {
            System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml",UriKind.Relative) });
            var frame = new TestFrame(new(new byte[80*80],80,80,8,1,80,DateTimeOffset.Now),"entry");
            var measured = Recorded(frame,new() { Channels=[new() { Channel="L",Reason="edge_angle_out_of_range" }] },new(10,10,60,60));
            var window = new CameraTestWindow(System.IO.Path.Combine(System.IO.Path.GetTempPath(),Guid.NewGuid()+".json"))
                { Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual,ShowInTaskbar=false,ShowActivated=false };
            MeasurementDetailWindow? details = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            try
            {
                window.Show();
                typeof(CameraTestWindow).GetField("_frame",flags)!.SetValue(window,frame);
                typeof(CameraTestWindow).GetField("_result",flags)!.SetValue(window,measured);
                var grid=(DataGrid)window.FindName("Metrics");grid.ItemsSource=measured.Rows();grid.SelectedIndex=0;
                var factory=typeof(CameraTestWindow).GetMethod("CreateSelectedMeasurementDetail",flags)!;
                details=Assert.IsType<MeasurementDetailWindow>(factory.Invoke(window,null));
                Assert.Same(measured,details.Snapshot.Measurement);
                Assert.Same(measured,typeof(CameraTestWindow).GetField("_result",flags)!.GetValue(window));
                typeof(CameraTestWindow).GetField("_frame",flags)!.SetValue(window,new TestFrame(frame.Data,"next"));
                Assert.Null(factory.Invoke(window,null));
            }
            finally { details?.Close();window.Close(); }
        });
    }

    [FieldImageTheory]
    [InlineData("01.tif",0)]
    [InlineData("Image_20260827102310051.bmp",1)]
    public void SuppliedImagesProduceMatchingDetailSnapshots(string filename, int sample)
    {
        var path=System.IO.Path.Combine(Environment.GetEnvironmentVariable("CAMERATEST_RESULT_IMAGES_DIR")!,filename);
        byte[] before;using(var stream=File.OpenRead(path)) before=SHA256.HashData(stream);
        var frame=TestFrame.Open(path);
        int[][] boxes=sample==0 ? [[650,250,500,510],[2690,240,530,540],[650,1420,510,500],[2690,1420,530,500],[1550,700,800,800]]
            : [[4200,650,1200,1000],[1720,2700,1100,1100],[4220,2650,1400,1200],[6500,2640,1300,1160],[4400,5230,1120,950]];
        var profile=new TestProfile { ImageWidth=frame.Data.Width,ImageHeight=frame.Data.Height,
            Regions=boxes.Select((r,i)=>new SearchRegion($"Point_{i+1}",r[0],r[1],r[2],r[3])).ToList() };
        var measured=FrameAnalysis.Run(frame,profile);
        Assert.Equal(5,measured.Targets.Count);
        Assert.All(measured.Targets,t=>Assert.True(t.Located,t.Id+": "+t.Reason));
        Assert.Contains(measured.Rows(),r=>r.Mtf50.HasValue);
        string? output=Environment.GetEnvironmentVariable("CAMERATEST_RESULT_CAPTURE_DIR");
        foreach(var row in new[] { measured.Rows().First(r=>r.Mtf50.HasValue),measured.Rows().FirstOrDefault(r=>r.ChannelAnalysis is {Valid:false}) }.OfType<MetricRow>())
        {
            var snapshot=new MeasurementDetailSnapshot(frame,measured,row.Target,row.Edge,.25);
            WpfTestHost.Invoke(() =>
            {
                System.Windows.Application.Current.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = new Uri("/ColorVision.Themes;component/Themes/Theme.xaml",UriKind.Relative) });
                var window=new MeasurementDetailWindow(snapshot,row.Channel) { Left=-20000,Top=-20000,WindowStartupLocation=WindowStartupLocation.Manual,ShowInTaskbar=false,ShowActivated=false };
                try
                {
                    window.Show();window.UpdateLayout();
                    Assert.Equal(MeasurementOverview.Format(row,0,.25),((TextBlock)window.FindName("Mtf50Value")).Text);
                    Assert.Same(row.Analysis,snapshot.Edge.Analysis);
                    if(output!=null)
                    {
                        Directory.CreateDirectory(output);
                        string tag=$"{System.IO.Path.GetFileNameWithoutExtension(filename)}-{row.Channel.Replace(" ","")}";
                        Capture(window,System.IO.Path.Combine(output,tag+".png"));
                        if(sample==0)
                        {
                            window.Width=960;window.Height=700;
                            ((Expander)window.FindName("QualityExpander")).IsExpanded=true;
                            window.UpdateLayout();
                            Capture(window,System.IO.Path.Combine(output,tag+"-expanded.png"));
                            var scroll=(ScrollViewer)window.FindName("DetailScroll");
                            Assert.True(scroll.ScrollableHeight>0);
                            scroll.ScrollToEnd();window.UpdateLayout();
                            Capture(window,System.IO.Path.Combine(output,tag+"-quality.png"));
                        }
                    }
                }
                finally { window.Close(); }
            });
        }
        if(output!=null) measured.Export(System.IO.Path.Combine(output,filename+".json"));
        using(var stream=File.OpenRead(path)) Assert.Equal(before,SHA256.HashData(stream));
    }

    private static void Capture(Window window, string path)
    {
        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(window);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}

public sealed class FieldImageTheoryAttribute : TheoryAttribute
{
    public FieldImageTheoryAttribute()
    {
        if(string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CAMERATEST_RESULT_IMAGES_DIR"))) Skip="Set CAMERATEST_RESULT_IMAGES_DIR to verify the supplied images.";
    }
}
