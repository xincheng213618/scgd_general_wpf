using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

internal sealed class BmwDrawingAnalysisState
{
    public BmwDrawingAnalysisState() { }
    private readonly ConditionalWeakTable<IRectangle, Identity> _identities = new();
    private readonly Dictionary<string, Identity> _known = new();
    private int _nextId;
    private DrawEditorContext? _draw;
    public SfrAnalysisOptions Options { get; set; } = new();
    public bool Busy { get; set; }
    private sealed class Identity(string id, IRectangle rectangle)
    {
        public string Id { get; } = id;
        public WeakReference<IRectangle> Rectangle { get; } = new(rectangle);
        public IAlgorithmOverlayRegistration? Overlay { get; set; }
        public void Clear() { Overlay?.Remove(); Overlay = null; }
        public void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Rect" or "Rotation") Clear();
        }
    }
    public void Bind(DrawEditorContext? draw)
    {
        if (draw == null || ReferenceEquals(draw,_draw)) return;
        if (_draw != null) CollectionChangedEventManager.RemoveHandler(_draw.DrawingVisualLists,DrawingsChanged);
        _draw=draw;
        CollectionChangedEventManager.AddHandler(draw.DrawingVisualLists,DrawingsChanged);
    }
    private void DrawingsChanged(object? sender,NotifyCollectionChangedEventArgs e)
    {
        foreach(var identity in _known.Values)
            if(!identity.Rectangle.TryGetTarget(out var rectangle)||_draw?.DrawingVisualLists.Any(v=>ReferenceEquals(v,rectangle))!=true) identity.Clear();
    }
    public IReadOnlyList<BmwSearchRegion> Capture(IEnumerable<IRectangle> rectangles,ImageSelectionScope scope)
    {
        return rectangles.Distinct(ReferenceEqualityComparer.Instance).Cast<IRectangle>().Select(rectangle=>
        {
            var identity=_identities.GetValue(rectangle,r=>
            {
                var entry=new Identity($"ROI_{++_nextId}",r); _known.Add(entry.Id,entry);
                if(r is DrawingVisualBase visual) PropertyChangedEventManager.AddHandler(visual.BaseAttribute,entry.Changed,string.Empty);
                else if(r is INotifyPropertyChanged observable) PropertyChangedEventManager.AddHandler(observable,entry.Changed,string.Empty);
                return entry;
            });
            return new BmwSearchRegion(identity.Id,PixelRoi(rectangle,scope));
        }).ToArray();
    }
    internal static RoiRect PixelRoi(IRectangle rectangle,ImageSelectionScope scope)
    {
        Rect bounds=rectangle is ISelectVisual visual?visual.GetRect():rectangle.Rect;
        if(bounds.IsEmpty||!new[]{bounds.Left,bounds.Top,bounds.Right,bounds.Bottom}.All(double.IsFinite))return default;
        try
        {
            int x=checked((int)Math.Floor(bounds.Left*scope.DpiX/96)), y=checked((int)Math.Floor(bounds.Top*scope.DpiY/96));
            int right=checked((int)Math.Ceiling(bounds.Right*scope.DpiX/96)),bottom=checked((int)Math.Ceiling(bounds.Bottom*scope.DpiY/96));
            return new(x,y,checked(right-x),checked(bottom-y));
        }
        catch(OverflowException) { return default; }
    }
    internal void ShowOverlays(ImageProcessingContext image,ImageSelectionScope scope,IReadOnlyList<BmwTargetAnalysis> results)
    {
        if(!TransientRoiSelectionSession.IsSourceScopeCurrent(image,scope))return;
        foreach(var target in results)
        {
            if(!_known.TryGetValue(target.Id,out var identity))continue;
            identity.Clear();
            if(!identity.Rectangle.TryGetTarget(out var rectangle)||!PixelRoi(rectangle,scope).Equals(target.SearchRoi))continue;
            if(_draw?.DrawingVisualLists.Any(v=>ReferenceEquals(v,rectangle))!=true)continue;
            identity.Overlay=BmwSfrOverlay.Apply(image,target,scope,_draw.ZoomRatio);
        }
    }
}

internal static class BmwDrawingAnalysisRunner
{
    private static readonly ConditionalWeakTable<ImageProcessingContext,BmwDrawingAnalysisState> States=new();
    internal static IRectangle[] SelectRectangles(DrawEditorContext? draw,IRectangle? clicked=null)
    {
        var selected=draw?.SelectionVisual?.SelectVisuals.OfType<IRectangle>().ToArray()??[];
        if(clicked!=null)return selected.Length>1&&selected.Any(r=>ReferenceEquals(r,clicked))?selected:[clicked];
        return selected.Length>0?selected:draw?.DrawingVisualLists.OfType<IRectangle>().ToArray()??[];
    }
    public static async void Run(ImageProcessingContext image,DrawEditorContext? draw,IEnumerable<IRectangle> rectangles)
    {
        var state=States.GetOrCreateValue(image); if(state.Busy)return;
        ImageFrameLease? lease=null;
        try
        {
            var scope=TransientRoiSelectionSession.CaptureSourceScope(image); if(scope==null)return;
            state.Bind(draw);
            var regions=state.Capture(rectangles,scope);
            if(regions.Count==0) { MessageBox.Show("请先在图像上绘制矩形，每框包含一个完整 BMW 靶标。", "BMW 四边 SFR"); return; }
            var options=state.Options with { };
            lease=image.AcquireImageFrame(); if(lease==null)return;
            state.Busy=true;
            var snapshot=lease;
            var results=await Task.Run(()=>BmwSfrAnalyzer.Analyze(snapshot.Image,regions,options));
            state.ShowOverlays(image,scope,results);
            var window=new BmwSfrResultWindow(snapshot,results,options,(updated,next)=>
            {
                state.Options=next with { };
                state.ShowOverlays(image,scope,updated);
            }) { Owner=Application.Current.GetActiveWindow() };
            window.Show(); lease=null;
        }
        catch(Exception ex) { MessageBox.Show(ex.Message,"BMW 四边 SFR",MessageBoxButton.OK,MessageBoxImage.Error); }
        finally { state.Busy=false; lease?.Dispose(); }
    }
}

public sealed class BmwSfrRectangleContextMenu(ImageProcessingContext image,DrawEditorContext draw) : IDVContextMenu
{
    public Type ContextType=>typeof(IRectangle);
    public IEnumerable<MenuItem> GetContextMenuItems(object obj)
    {
        if(obj is not IRectangle rectangle)return [];
        var run=new MenuItem { Header="BMW 四边 SFR" };
        run.Click+=(_,_)=>BmwDrawingAnalysisRunner.Run(image,draw,BmwDrawingAnalysisRunner.SelectRectangles(draw,rectangle));
        return [run];
    }
}
