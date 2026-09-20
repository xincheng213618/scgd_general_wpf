using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;
using ColorVision.Themes;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class BmwSfrUiTests
{
    [Fact]
    public void TargetCenterUsesDetectedCoordinatesAndFixedScreenSizeWithIndependentVisibility()
    {
        WpfTestHost.Invoke(() =>
        {
            var target = new BmwTargetAnalysis("P", new(0, 0, 800, 600), true, "", default, 245.5, 176.25, []);
            var scope = new ImageSelectionScope(Guid.NewGuid(), 1, 800, 600, 144, 120);
            var settings = new BmwSfrOverlaySettings { ShowPointNames = false };
            static IEnumerable<GeometryDrawing> Geometries(Drawing drawing) => drawing is DrawingGroup group ? group.Children.SelectMany(Geometries)
                : drawing is GeometryDrawing geometry ? [geometry] : [];
            foreach (double zoom in new[] { .4, 1.2 })
            {
                var lines = Geometries(BmwSfrOverlayRenderer.CreateVisual(target, scope, zoom, "L", settings, drawSearch: false).Drawing)
                    .Select(d => Assert.IsType<LineGeometry>(d.Geometry)).ToArray();
                Assert.Equal(2, lines.Length);
                Assert.Equal(target.CenterX * 96 / 144, (lines[0].StartPoint.X + lines[0].EndPoint.X) / 2, 6);
                Assert.Equal(target.CenterY * 96 / 120, lines[0].StartPoint.Y, 6);
                Assert.Equal(12, (lines[0].EndPoint.X - lines[0].StartPoint.X) * zoom, 6);
            }
            settings.ShowTargetCenter = false;
            Assert.Null(BmwSfrOverlayRenderer.CreateVisual(target, scope, 1, "L", settings, drawSearch: false).Drawing);
            settings.ShowCenterCoordinates = true;
            Assert.NotEmpty(BmwSfrOverlayRenderer.CreateVisual(target, scope, 1, "L", settings, drawSearch: false).Drawing.Children);
            settings.ShowTargetCenter = true;
            Assert.Null(BmwSfrOverlayRenderer.CreateVisual(target with { Located = false }, scope, 1, "L", settings, drawSearch: false).Drawing);
        });
    }

    [Fact]
    public void GeometryLabelsUseActualRoiAndDetectedCenterEvenWhenMetricIsHidden()
    {
        var edge = new BmwEdgeAnalysis(BmwEdgeId.Left, new(100, 170, 80, 60), false, "", null);
        var target = new BmwTargetAnalysis("P", new(0, 0, 700, 500), true, "", default, 200, 200, [edge]);
        var settings = new BmwSfrOverlaySettings { ShowValues = false, ShowEdgeNames = false, ShowRoiDimensions = true, ShowCenterDistance = true };
        string label = BmwSfrPresentation.OverlayLabel(edge, "L", settings, target);
        Assert.Contains("80×60 px", label);
        Assert.Contains($"距中心 {60:F1} px", label);
        Assert.DoesNotContain("MTF", label);
        Assert.DoesNotContain("距中心", BmwSfrPresentation.OverlayLabel(edge, "L", settings, target with { Located = false }));
        settings.ShowRoiDimensions = settings.ShowCenterDistance = false;
        Assert.Empty(BmwSfrPresentation.OverlayLabel(edge, "L", settings, target));
    }

    [Fact]
    public void OverlayMetricQueriesExistingCurveAndNeverSubstitutesAnInvalidChannel()
    {
        var edge = new BmwEdgeAnalysis(BmwEdgeId.Top, new(0, 0, 60, 80), true, "", new SfrAnalysisResult
        { Channels = [new() { Channel = "G", Valid = true, Mtf50 = .2, Mtf10 = .45, Frequencies = [0, .2, .3, .5], Mtf = [1, .5, .3, .05] }] });
        var settings = new BmwSfrOverlaySettings { Metric = BmwSfrDisplayMetric.AtFrequency, Frequency = .25 };
        Assert.Contains("MTF@0.25", BmwSfrPresentation.OverlayLabel(edge, "G", settings));
        Assert.Contains(.4.ToString("P1"), BmwSfrPresentation.OverlayLabel(edge, "G", settings));
        Assert.Contains("INVALID", BmwSfrPresentation.OverlayLabel(edge, "R", settings));
        settings.Metric = BmwSfrDisplayMetric.Mtf10;
        Assert.Contains("MTF10 0.4500", BmwSfrPresentation.OverlayLabel(edge, "G", settings));
        settings.Metric = BmwSfrDisplayMetric.AtNyquist;
        Assert.Contains(.05.ToString("P1"), BmwSfrPresentation.OverlayLabel(edge, "G", settings));
        Assert.Equal(settings.Metric, settings.Copy().Metric);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FittedBladeUsesDashedMeasuredCoordinatesAndCanBeHidden(bool rotated)
    {
        WpfTestHost.Invoke(() =>
        {
            var roi = new RoiRect(100, 200, 60, 80);
            var edge = new BmwEdgeAnalysis(BmwEdgeId.Left, roi, true, "", new SfrAnalysisResult
            { Channels = [new() { Channel = "L", Valid = true, FitAvailable = true, Rotated = rotated, EdgeIntercept = 20, EdgeSlope = .1 }] });
            var target = new BmwTargetAnalysis("P", new(0, 0, 400, 400), true, "", default, 0, 0, [edge]);
            var scope = new ImageSelectionScope(Guid.NewGuid(), 1, 400, 400, 144, 120);
            static IEnumerable<GeometryDrawing> Geometries(Drawing drawing) => drawing is DrawingGroup group ? group.Children.SelectMany(Geometries)
                : drawing is GeometryDrawing geometry ? [geometry] : [];
            var settings = new BmwSfrOverlaySettings();
            var visual = BmwSfrOverlayRenderer.CreateVisual(target, scope, .5, "L", settings);
            var dashed = Assert.Single(Geometries(visual.Drawing).Where(d => d.Pen?.DashStyle.Dashes.Count > 0));
            var line = Assert.IsType<LineGeometry>(dashed.Geometry);
            Assert.Equal((rotated ? 159 : 120) * 96d / 144, line.StartPoint.X, 6);
            Assert.Equal((rotated ? 220 : 200) * 96d / 120, line.StartPoint.Y, 6);
            Assert.Equal((rotated ? 100 : 127.9) * 96d / 144, line.EndPoint.X, 6);
            Assert.Equal((rotated ? 225.9 : 279) * 96d / 120, line.EndPoint.Y, 6);
            settings.ShowFittedEdges = false;
            Assert.DoesNotContain(Geometries(BmwSfrOverlayRenderer.CreateVisual(target, scope, .5, "L", settings).Drawing), d => d.Pen?.DashStyle.Dashes.Count > 0);
        });
    }
    [Fact]
    public void EditedRoiMustStayInItsOwnOuterSearchBox()
    {
        Assert.True(BmwSfrResultWindow.IsInside(new(120,130,80,60),new(100,100,300,300)));
        Assert.False(BmwSfrResultWindow.IsInside(new(80,130,80,60),new(100,100,300,300)));
        Assert.False(BmwSfrResultWindow.IsInside(new(120,130,0,60),new(100,100,300,300)));
    }
    [Fact]
    public void PartialChannelFailureKeepsLuminanceMetricAndExplainsMissingCurves()
    {
        var edge=new BmwEdgeAnalysis(BmwEdgeId.Left,new(0,0,60,80),false,"R:textured_or_noisy_plateaus",
            new SfrAnalysisResult { Channels=[new() { Channel="L",Valid=true,Mtf50=.25 },new() { Channel="R",Valid=false,Reason="textured_or_noisy_plateaus" }] });
        Assert.Equal("1/2 部分可计算",BmwSfrPresentation.State(edge));
        Assert.Contains("0.2500",BmwSfrPresentation.OverlayLabel(edge,"L"));
        Assert.Equal("左  R MTF50 INVALID",BmwSfrPresentation.OverlayLabel(edge,"R"));
        Assert.Equal("左  B MTF50 INVALID",BmwSfrPresentation.OverlayLabel(edge,"B"));
        Assert.Contains("平台纹理",BmwSfrPresentation.ChannelState(edge,"R"));
        Assert.Null(BmwSfrPresentation.Mtf50(edge,"R"));
    }
    [Fact]
    public void MainOverlayShowsMtf50AndIsRemovedWhenTheRectangleMovesOrSourceChanges()
    {
        WpfTestHost.Invoke(()=>
        {
            var app=Application.Current;
            app.Resources["TextBox.Small"]=new Style(typeof(TextBox)); app.Resources["ComboBox.Small"]=new Style(typeof(ComboBox));
            app.Resources["ToolBarBaseStyle"]=new Style(typeof(ToolBar)); app.Resources["ToolBarImage"]=new Style(typeof(System.Windows.Controls.Image));
            app.Resources["BaseStyle"]=new Style(typeof(Control)); app.Resources["RangeSliderBaseStyle"]=new Style(typeof(HandyControl.Controls.RangeSlider));
            app.Resources["bool2VisibilityConverter"]=new BooleanToVisibilityConverter();
            using var view=new ImageView();
            view.SetImageSource(new WriteableBitmap(640,480,96,96,PixelFormats.Gray8,null),enableEditorImageServices:false,configureDefaultLayerController:false);
            var image=view.EditorContext.ProcessingContext; var draw=view.EditorContext.DrawEditorContext;
            var rectangle=new DVRectangle { Rect=new(100,100,200,200) }; draw.DrawingVisualLists.Add(rectangle);
            var state=new BmwDrawingAnalysisState(); state.Bind(draw);
            var scope=TransientRoiSelectionSession.CaptureSourceScope(image)!;
            var request=state.Capture([rectangle],scope)[0];
            var edge=new BmwEdgeAnalysis(BmwEdgeId.Left,new(130,140,40,60),true,"",new SfrAnalysisResult { Channels=[new() { Channel="L",Valid=true,Mtf50=.25 },new() { Channel="R",Valid=true,Mtf50=.18 }] });
            var target=new BmwTargetAnalysis(request.Id,request.Roi,true,"",request.Roi,200,200,[edge]);
            state.ShowOverlays(image,scope,[target]);
            var overlay=Assert.Single(image.AlgorithmOverlays.Snapshot());
            Assert.Contains("MTF50 0.2500",Assert.Single(overlay.Items).Style.Label);
            Assert.Single(image.SnapshotAlgorithmOverlayRegistrations());
            state.DisplayChannel="R"; state.ShowOverlays(image,scope,[target]);
            Assert.Equal("左  R MTF50 0.1800",Assert.Single(Assert.Single(image.AlgorithmOverlays.Snapshot()).Items).Style.Label);
            state.DisplayChannel="B"; state.ShowOverlays(image,scope,[target]);
            Assert.Equal("左  B MTF50 INVALID",Assert.Single(Assert.Single(image.AlgorithmOverlays.Snapshot()).Items).Style.Label);
            rectangle.Rect=new(110,100,200,200);
            Assert.Empty(image.AlgorithmOverlays.Snapshot());
            rectangle.Rect=new(100,100,200,200);
            state.ShowOverlays(image,scope,[target]); Assert.Single(image.AlgorithmOverlays.Snapshot());
            image.NotifySourcePixelsChanged(); Assert.Empty(image.AlgorithmOverlays.Snapshot());
            state.ShowOverlays(image,scope,[target]); Assert.Empty(image.AlgorithmOverlays.Snapshot());
        });
    }
    [SfrNativeFact]
    public async Task InnerRoiCanBeSelectedInsideOuterRoiAndRecomputedWithoutBecomingAnotherSearchBox()
    {
        await WpfTestHost.Invoke<Task>(async()=>
        {
            PrepareImageViewResources();
            using var view=new ImageView();
            const int width=360,height=280;
            var pixels=new byte[width*height*3];
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)for(int c=0;c<3;c++)
                pixels[(y*width+x)*3+c]=(byte)(30+190/(1+Math.Exp(-(x-138-y*.08)/1.3)));
            var bitmap=new WriteableBitmap(BitmapSource.Create(width,height,144,120,PixelFormats.Bgr24,null,pixels,width*3));
            view.SetImageSource(bitmap,enableEditorImageServices:false,configureDefaultLayerController:false);
            var image=view.EditorContext.ProcessingContext; var draw=view.EditorContext.DrawEditorContext;
            var outer=new DVRectangle { Rect=new(0,0,240,224) }; draw.DrawCanvas.AddVisual(outer);
            var state=new BmwDrawingAnalysisState(); state.Bind(draw);
            var scope=TransientRoiSelectionSession.CaptureSourceScope(image)!;
            var request=state.Capture([outer],scope)[0];
            using var lease=image.AcquireImageFrame(); Assert.NotNull(lease);
            var first=BmwSfrResultWindow.AnalyzeEdge(lease.Image,new(BmwEdgeId.Left,new(110,50,80,150),false,"",null),new());
            var sibling=first with { Id=BmwEdgeId.Right };
            var target=new BmwTargetAnalysis(request.Id,request.Roi,true,"",request.Roi,150,140,[first,sibling]);
            state.ShowOverlays(image,scope,[target]);
            draw.SelectionVisual.SetRender(outer);
            var click=new Point(150*96d/144,120*96d/120);
            Assert.True(state.SelectEdgeAt(click));
            var handle=Assert.IsType<BmwSfrEdgeSelectionVisual>(draw.SelectionVisual.PrimarySelectedVisual);
            Assert.Equal(BmwEdgeId.Left,handle.Edge);
            Assert.Same(draw.SelectionVisual,draw.DrawCanvas.GetVisual<Visual>(click));
            Assert.Single(draw.DrawingVisualLists);
            Assert.Same(outer,Assert.Single(BmwDrawingAnalysisRunner.SelectRectangles(draw)));
            var oldOverlay=Assert.Single(image.SnapshotAlgorithmOverlayRegistrations()).Visual;
            draw.Zoombox.Zoom(new Point(0,0),new Vector(1.5,1.5));
            Assert.NotSame(oldOverlay,Assert.Single(image.SnapshotAlgorithmOverlayRegistrations()).Visual);
            Assert.Same(handle,state.FindEdgeAt(click));
            Assert.Same(target,handle.Owner.Target);
            var original=handle.GetRect();
            handle.SetRect(new(original.X+4,original.Y,original.Width+8,original.Height));
            var pending=handle.Owner.Target.Edges[0];
            Assert.Equal(new RoiRect(116,50,92,150),pending.Roi);
            Assert.Null(pending.Analysis);
            Assert.Contains("INVALID",Assert.Single(image.AlgorithmOverlays.Snapshot()).Items[0].Style.Label);
            await handle.Owner.FlushPendingAsync();
            var updated=handle.Owner.Target;
            Assert.Equal(request.Id,updated.Id);
            Assert.Equal(pending.Roi,updated.Edges[0].Roi);
            Assert.True(updated.Edges[0].Analysis!=null,updated.Edges[0].Reason);
            Assert.Equal(4,updated.Edges[0].Analysis!.Channels.Count);
            Assert.Same(sibling,updated.Edges[1]);
            Assert.Equal(new Rect(0,0,240,224),outer.Rect);
            handle.SetRect(new(-100,-100,10000,10000));
            Assert.Equal(request.Roi,handle.Owner.Target.Edges[0].Roi);
            image.NotifySourcePixelsChanged();
            await handle.Owner.FlushPendingAsync();
            Assert.Null(state.FindEdgeAt(click));
            Assert.Empty(draw.SelectionVisual.SelectVisuals);
            Assert.Empty(image.AlgorithmOverlays.Snapshot());
        });
    }

    private static void PrepareImageViewResources()
    {
        var app=Application.Current;
        app.Resources["TextBox.Small"]=new Style(typeof(TextBox)); app.Resources["ComboBox.Small"]=new Style(typeof(ComboBox));
        app.Resources["ToolBarBaseStyle"]=new Style(typeof(ToolBar)); app.Resources["ToolBarImage"]=new Style(typeof(System.Windows.Controls.Image));
        app.Resources["BaseStyle"]=new Style(typeof(Control)); app.Resources["RangeSliderBaseStyle"]=new Style(typeof(HandyControl.Controls.RangeSlider));
        app.Resources["bool2VisibilityConverter"]=new BooleanToVisibilityConverter();
    }

    [SfrNativeFact]
    public void DetailedWindowAndEditedEdgePreserveFourChannelRowsAndOriginalIdentity()
    {
        WpfTestHost.Invoke(()=>
        {
            string? source=Environment.GetEnvironmentVariable("COLORVISION_BMW_UI_IMAGE");
            HImage image;
            RoiRect search;
            if(!string.IsNullOrEmpty(source))
            {
                image=new BitmapImage(new Uri(source)).ToHImage();
                search=new(650,250,500,510);
            }
            else
            {
                const int size=480;
                byte[] pixels=Enumerable.Repeat((byte)220,size*size*3).ToArray();
                double a=5*Math.PI/180;
                for(int y=0;y<size;y++)for(int x=0;x<size;x++)
                {
                    double dx=x-240,dy=y-240,u=dx*Math.Cos(a)+dy*Math.Sin(a),v=-dx*Math.Sin(a)+dy*Math.Cos(a);
                    if(u*u+v*v<180*180 && u*v>0)for(int c=0;c<3;c++)pixels[(y*size+x)*3+c]=25;
                }
                var pointer=Marshal.AllocCoTaskMem(pixels.Length); Marshal.Copy(pixels,0,pointer,pixels.Length);
                image=new HImage { rows=size,cols=size,channels=3,depth=8,stride=size*3,pData=pointer };
                search=new(0,0,size,size);
            }
            var frame=new SourceImageFrame(image,1,h=>h.Dispose());
            var lease=frame.Acquire(); frame.Dispose();
            var options=new SfrAnalysisOptions();
            var results=BmwSfrAnalyzer.Analyze(image,[new("ROI_7",search)],options);
            Assert.True(results[0].Located);
            var edge=results[0].Edges[0]; var resized=edge.Roi; resized.Width+=4;
            var edited=BmwSfrResultWindow.AnalyzeEdge(image,edge with { Roi=resized },options);
            Assert.Equal(edge.Id,edited.Id); Assert.Equal(resized,edited.Roi); Assert.Equal(4,edited.Analysis!.Channels.Count);
            var adjusted=results.Select(t=>t with { Edges=t.Edges.Select(e=>e.Id==edited.Id?edited:e).ToArray() }).ToArray();
            var again=BmwSfrResultWindow.Reanalyze(image,adjusted,options);
            Assert.Equal(resized,again[0].Edges[0].Roi); Assert.Equal("ROI_7",again[0].Id);
            var window=new BmwSfrResultWindow(lease,again,options) { ShowInTaskbar=false,ShowActivated=false,Left=-20000,Top=-20000 };
            var oldTheme=ThemeManager.Current.CurrentTheme??Theme.Light;
            try
            {
                window.Show(); window.UpdateLayout();
                Assert.Equal(4,((DataGrid)window.FindName("ResultsGrid")).Items.Count);
                Assert.Equal(4,((DataGrid)window.FindName("ChannelsGrid")).Items.Count);
                Assert.Contains("MTF50",((TextBlock)window.FindName("Mtf50Text")).Text);
                var selector=(ComboBox)window.FindName("DisplayChannelSelector");
                var grid=(DataGrid)window.FindName("ResultsGrid");
                var selectedEdge=Assert.IsType<BmwSfrResultWindow.EdgeRow>(grid.SelectedItem).Edge;
                var displayedChannels=new List<string>();
                window.DisplayUpdated+=(unchanged,_,channel)=>
                {
                    Assert.Same(again,unchanged);
                    displayedChannels.Add(channel);
                };
                foreach(string channel in new[]{"R","G","B","L"})
                {
                    selector.SelectedValue=channel;
                    Assert.Equal(channel,window.DisplayChannel);
                    Assert.Same(selectedEdge,Assert.IsType<BmwSfrResultWindow.EdgeRow>(grid.SelectedItem).Edge);
                    Assert.Equal($"{channel} MTF50",grid.Columns[2].Header);
                    Assert.StartsWith($"{channel} MTF50",((TextBlock)window.FindName("Mtf50Text")).Text);
                    Assert.Equal(4,((DataGrid)window.FindName("ChannelsGrid")).Items.Count);
                    if(channel=="R" && edited.Analysis.Channels.Single(c=>c.Channel=="R").Valid==false)
                        Assert.Contains("—",((TextBlock)window.FindName("Mtf50Text")).Text);
                }
                Assert.Equal(new[]{"R","G","B","L"},displayedChannels);
                string? directory=Environment.GetEnvironmentVariable("COLORVISION_BMW_UI_OUTPUT");
                if(!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                    if (!string.IsNullOrEmpty(source)) SaveMainOverlayEvidence(image, source, directory, options);
                    foreach(var theme in new[]{Theme.Light,Theme.Dark})
                    {
                        ThemeManager.Current.ApplyTheme(Application.Current,theme);
                        window.UpdateLayout();
                        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Render);
                        var bitmap=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);
                        bitmap.Render(window);
                        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                        using var output=File.Create(Path.Combine(directory,$"bmw-window-{theme}.png")); encoder.Save(output);
                    }
                }
            }
            finally { window.Close(); ThemeManager.Current.ApplyTheme(Application.Current,oldTheme); }
        });
    }

    private static void SaveMainOverlayEvidence(HImage image, string source, string directory, SfrAnalysisOptions options)
    {
        var targets = BmwSfrAnalyzer.Analyze(image,
            [new("ROI_1",new(650,250,500,510)),new("ROI_2",new(2700,250,510,510)),
             new("ROI_3",new(650,1420,500,500)),new("ROI_4",new(2700,1410,510,500))],options);
        var scope = new ImageSelectionScope(Guid.NewGuid(),1,image.cols,image.rows,96,96);
        double scale = 1400d / image.cols;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(scale,scale));
            dc.DrawImage(new BitmapImage(new Uri(source)),new Rect(0,0,image.cols,image.rows));
            foreach (var target in targets)
            {
                dc.DrawDrawing(BmwSfrOverlay.CreateVisual(target,scope,scale,"L").Drawing);
            }
            dc.Pop();
        }
        var bitmap = new RenderTargetBitmap(1400,(int)Math.Ceiling(image.rows*scale),96,96,PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory,"bmw-main-overlay.png")); encoder.Save(output);
    }
}
