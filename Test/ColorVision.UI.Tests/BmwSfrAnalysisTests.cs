using ColorVision.Core;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class BmwSfrAnalysisTests
{
    [Fact]
    public void InvalidSearchRegionsRetainCallerIdentityAndAllFourEdges()
    {
        var image = new HImage { rows = 100, cols = 100 };
        var result = BmwSfrAnalyzer.Analyze(image, [new("missing-7", new(-1,0,40,40)), new("missing-12", default)], new());
        Assert.Equal(new[] { "missing-7", "missing-12" }, result.Select(r => r.Id));
        foreach (var target in result)
        {
            Assert.False(target.Located);
            Assert.Equal("invalid_search_roi", target.Reason);
            Assert.Equal(Enum.GetValues<BmwEdgeId>(), target.Edges.Select(e => e.Id));
            Assert.All(target.Edges, e => { Assert.False(e.Valid); Assert.Null(e.Analysis); Assert.Equal(target.Reason,e.Reason); });
        }
    }

    [Fact]
    public void DuplicateOrEmptyIdentityAndInvalidQualityOptionsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => BmwSfrAnalyzer.Analyze(default,[new("a",default),new("a",default)],new()));
        Assert.Throws<ArgumentException>(() => BmwSfrAnalyzer.Analyze(default,[new(" ",default)],new()));
        Assert.Throws<ArgumentException>(() => BmwSfrAnalyzer.Analyze(default,[],new() { MaximumFitRms = double.NaN }));
        Assert.Throws<ArgumentException>(() => BmwSfrAnalyzer.Analyze(default,[],new(),new() { ChartType = (SfrChartType)99 }));
    }

    [SfrNativeFact]
    public void CheckerboardTypeAndSupportLimitsSurviveManagedAnalysis()
    {
        const int size = 720;
        byte[] pixels = new byte[size * size];
        double angle = 5 * Math.PI / 180;
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            double dx = x - 360, dy = y - 360;
            int u = (int)Math.Floor((dx * Math.Cos(angle) + dy * Math.Sin(angle)) / 180), v = (int)Math.Floor((-dx * Math.Sin(angle) + dy * Math.Cos(angle)) / 180);
            pixels[y * size + x] = (byte)((u + v) % 2 == 0 ? 40 : 200);
        }
        IntPtr ptr = Marshal.AllocHGlobal(pixels.Length);
        try
        {
            Marshal.Copy(pixels,0,ptr,pixels.Length);
            var image = new HImage { rows=size,cols=size,channels=1,depth=8,stride=size,pData=ptr,isDispose=true };
            var regions = new BmwSearchRegion[] { new("chess",new(60,60,600,600)) };
            foreach (var type in new[] { SfrChartType.Checkerboard, SfrChartType.Auto })
            {
                var result = Assert.Single(BmwSfrAnalyzer.Analyze(image,regions,new(),new() { ChartType=type }));
                Assert.True(result.Located);
                Assert.Equal(SfrChartType.Checkerboard,result.DetectedChartType);
                Assert.All(result.Edges,e => { Assert.True(e.SupportRoi.Width>0); Assert.Single(e.Analysis!.Channels); });
            }
            var oversized = Assert.Single(BmwSfrAnalyzer.Analyze(image,regions,new(),new() { ChartType=SfrChartType.Checkerboard,AlongEdgePixels=400 }));
            Assert.All(oversized.Edges,e => { Assert.Null(e.Analysis); Assert.Equal("checkerboard_roi_crosses_junction",e.Reason); });
            var partial = Assert.Single(BmwSfrAnalyzer.Analyze(image, [new("narrow",new(293,250,160,230))], new(), new() { ChartType=SfrChartType.Checkerboard }));
            Assert.True(partial.Located);
            Assert.Equal(SfrChartType.Checkerboard, partial.DetectedChartType);
            Assert.Equal(4, partial.Edges.Count);
            Assert.Contains(partial.Edges, e => e.Analysis != null);
            Assert.Contains(partial.Edges, e => e.Analysis == null && !e.Valid && e.Reason == "checkerboard_insufficient_edge_support");
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }

    [SfrNativeFact]
    public void BlankSearchBetweenTargetsIsNotRenumberedAndColorChannelsRemainIndependent()
    {
        const int size=480, width=1440;
        byte[] pixels=Enumerable.Repeat((byte)220,width*size*3).ToArray();
        double a=5*Math.PI/180;
        for(int y=0;y<size;y++) for(int x=0;x<width;x++)
        {
            if(x/size==1) continue;
            double dx=x%size-240,dy=y-240,u=dx*Math.Cos(a)+dy*Math.Sin(a),v=-dx*Math.Sin(a)+dy*Math.Cos(a);
            if(u*u+v*v<180*180 && u*v>0) for(int c=0;c<3;c++) pixels[(y*width+x)*3+c]=25;
        }
        IntPtr ptr=Marshal.AllocHGlobal(pixels.Length);
        try
        {
            Marshal.Copy(pixels,0,ptr,pixels.Length);
            var image=new HImage { rows=size,cols=width,channels=3,depth=8,stride=width*3,pData=ptr,isDispose=true };
            var results=BmwSfrAnalyzer.Analyze(image,[new("A",new(0,0,size,size)),new("B",new(size,0,size,size)),new("C",new(2*size,0,size,size))],new() { InputEncoding=SfrInputEncoding.Linear });
            Assert.Equal(new[]{"A","B","C"},results.Select(r=>r.Id));
            Assert.True(results[0].Located); Assert.False(results[1].Located); Assert.True(results[2].Located);
            Assert.All(results,r=>Assert.Equal(Enum.GetValues<BmwEdgeId>(),r.Edges.Select(e=>e.Id)));
            Assert.All(results[1].Edges,e=>Assert.Null(e.Analysis));
            Assert.All(results[2].Edges,e=>
            {
                Assert.NotNull(e.Analysis);
                Assert.Equal(new[]{"B","G","L","R"},e.Analysis.Channels.Select(c=>c.Channel).Order());
                Assert.True(e.Roi.X>=2*size);
                Assert.NotNull(SfrAnalysisResult.Parse(System.Text.Json.JsonSerializer.Serialize(e.Analysis)));
            });
        }
        finally { Marshal.FreeHGlobal(ptr); }
    }
    [Fact]
    public void ResultWindowRetainsInvalidTabsAndReleasesItsSnapshot()
    {
        WpfTestHost.Invoke(() =>
        {
            var ptr=Marshal.AllocHGlobal(100*100);
            Marshal.Copy(new byte[100*100],0,ptr,100*100);
            int released=0;
            var frame=new SourceImageFrame(new HImage { rows=100,cols=100,channels=1,depth=8,stride=100,pData=ptr },42,h=> { Marshal.FreeHGlobal(h.pData); released++; });
            var lease=frame.Acquire(); frame.Dispose();
            var edges=Enum.GetValues<BmwEdgeId>().Select(id=>new BmwEdgeAnalysis(id,default,false,"target_not_found",null)).ToArray();
            var result=new BmwTargetAnalysis("fixed-9",new(0,0,100,100),false,"target_not_found",default,0,0,edges);
            var type=typeof(ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR.BmwSfrResultWindow);
            var window=(System.Windows.Window)Activator.CreateInstance(type,lease,new[]{result},new SfrAnalysisOptions(),null)!;
            window.ShowInTaskbar=false; window.ShowActivated=false; window.Left=-20000; window.Top=-20000;
            try
            {
                window.Show(); window.UpdateLayout();
                var rows=(System.Windows.Controls.DataGrid)window.FindName("ResultsGrid");
                Assert.Equal(4,rows.Items.Count);
                Assert.Equal("fixed-9",((ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR.BmwSfrResultWindow.EdgeRow)rows.Items[0]).TargetId);
                Assert.Equal(4,((System.Windows.Controls.DataGrid)window.FindName("ChannelsGrid")).Items.Count);
                Assert.Equal(0,released);
            }
            finally { window.Close(); }
            Assert.Equal(1,released);
        });
    }}
