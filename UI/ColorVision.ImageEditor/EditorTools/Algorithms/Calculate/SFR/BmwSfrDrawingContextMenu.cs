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
using System.Windows.Input;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

internal sealed class BmwDrawingAnalysisState
{
    public BmwDrawingAnalysisState() { }
    private readonly ConditionalWeakTable<IRectangle, Identity> _identities = new();
    private readonly Dictionary<string, Identity> _known = new();
    private int _nextId;
    private DrawEditorContext? _draw;
    private ImageProcessingContext? _image;
    private ImageSelectionScope? _scope;
    public SfrAnalysisOptions Options { get; set; } = new();
    public BmwSfrRoiSettings MeasurementRoi { get; set; } = new();
    public string DisplayChannel { get; set; } = "L";
    public BmwSfrOverlaySettings DisplaySettings { get; set; } = new();
    public bool Busy { get; set; }
    private sealed class Identity(string id, IRectangle rectangle)
    {
        public string Id { get; } = id;
        public WeakReference<IRectangle> Rectangle { get; } = new(rectangle);
        public IAlgorithmOverlayRegistration? Overlay { get; set; }
        public BmwSfrRoiInteraction? Interaction { get; set; }
        public BmwTargetAnalysis? Target { get; set; }
        public WeakReference<BmwSfrResultWindow>? Window { get; set; }
        public void Clear() { Overlay?.Remove(); Overlay = null; Interaction?.Dispose(); Interaction=null; Target=null; Window=null; }
        public void Changed(object? sender, PropertyChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName is "Rect" or "Rotation") Clear();
        }
    }
    public void Bind(DrawEditorContext? draw)
    {
        if (draw == null || ReferenceEquals(draw,_draw)) return;
        if (_draw != null)
        {
            foreach(var identity in _known.Values) identity.Clear();
            CollectionChangedEventManager.RemoveHandler(_draw.DrawingVisualLists,DrawingsChanged);
            _draw.Zoombox.PreviewMouseLeftButtonDown-=PreviewMouseDown;
            _draw.Zoombox.ContentMatrixChanged-=ZoomChanged;
            _draw.DrawCanvas.VisualsChanged-=CanvasChanged;
        }
        _draw=draw;
        CollectionChangedEventManager.AddHandler(draw.DrawingVisualLists,DrawingsChanged);
        draw.Zoombox.PreviewMouseLeftButtonDown+=PreviewMouseDown;
        draw.Zoombox.ContentMatrixChanged+=ZoomChanged;
        draw.DrawCanvas.VisualsChanged+=CanvasChanged;
    }
    private void CanvasChanged(object? sender,VisualChangedEventArgs e)
    {
        if(e.ChangeType==VisualChangeType.Clear) foreach(var identity in _known.Values) identity.Clear();
    }
    private void ZoomChanged(object? sender,EventArgs e)
    {
        if(_image==null || _scope==null)return;
        ShowOverlays(_image,_scope,_known.Values.Where(i=>i.Target!=null).Select(i=>i.Target!).ToArray());
    }
    private void SourceChanged(object? sender,EventArgs e)
    {
        if(_image==null || _scope==null || TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope))return;
        foreach(var identity in _known.Values) identity.Clear();
    }
    private void PreviewMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(_draw==null || !_draw.IsImageEditMode || _draw.DrawEditorManager.Current!=null)return;
        if(_draw.Zoombox.ActivateOn!=ModifierKeys.None && Keyboard.Modifiers.HasFlag(_draw.Zoombox.ActivateOn))return;
        Point point=e.GetPosition(_draw.DrawCanvas);
        if(e.ClickCount==1 && _draw.SelectionVisual.PrimarySelectedVisual is BmwSfrEdgeSelectionVisual && _draw.SelectionVisual.GetContainingRect(point))return;
        if(SelectEdgeAt(point,e.ClickCount==2) && e.ClickCount==2)e.Handled=true;
    }
    internal BmwSfrEdgeSelectionVisual? FindEdgeAt(Point point) => _known.Values.Where(i=>i.Interaction!=null)
        .SelectMany(i=>i.Interaction!.Handles).Where(v=>v.GetRect().Contains(point)).OrderBy(v=>v.GetRect().Width*v.GetRect().Height).FirstOrDefault();
    internal bool SelectEdgeAt(Point point,bool showDetails=false)
    {
        if(_image==null || _draw==null || _scope==null || !TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope))return false;
        var visual=FindEdgeAt(point); if(visual==null)return false;
        _draw.SelectionVisual.SetRender(visual);
        var target=visual.Owner.Target;
        var identity=_known[target.Id];
        if(identity.Window?.TryGetTarget(out var window)==true && !window.IsClosed)
        {
            window.SelectEdge(target.Id,visual.Edge);
            if(showDetails)window.Activate();
        }
        else if(showDetails) BmwDrawingAnalysisRunner.OpenWindow(_image,this,_scope,[target],target.Id,visual.Edge);
        return true;
    }
    internal void TrackWindow(BmwSfrResultWindow window,IReadOnlyList<BmwTargetAnalysis> targets)
    {
        foreach(var target in targets) if(_known.TryGetValue(target.Id,out var identity))identity.Window=new(window);
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
        if(!ReferenceEquals(_image,image))
        {
            if(_image!=null)_image.DocumentScopeChanged-=SourceChanged;
            _image=image; image.DocumentScopeChanged+=SourceChanged;
        }
        _scope=scope;
        foreach(var target in results)
        {
            if(!_known.TryGetValue(target.Id,out var identity))continue;
            if(!identity.Rectangle.TryGetTarget(out var rectangle)||!PixelRoi(rectangle,scope).Equals(target.SearchRoi)
                ||_draw?.DrawingVisualLists.Any(v=>ReferenceEquals(v,rectangle))!=true) { identity.Clear(); continue; }
            identity.Overlay?.Remove();
            identity.Target=target;
            identity.Overlay=BmwSfrOverlay.Apply(image,target,scope,_draw.ZoomRatio,DisplayChannel,DisplaySettings);
            if(target.Located)
            {
                if(identity.Interaction==null)identity.Interaction=new(image,_draw,scope,target,()=>Options,updated=>ShowOverlays(image,scope,[updated]));
                else identity.Interaction.UpdateTarget(target);
            }
            else { identity.Interaction?.Dispose(); identity.Interaction=null; }
            if(identity.Window?.TryGetTarget(out var window)==true && !window.IsClosed)window.UpdateTarget(target);
        }
        if(_draw?.SelectionVisual?.PrimarySelectedVisual is BmwSfrEdgeSelectionVisual)
            _draw.DrawCanvas.TopVisual(_draw.SelectionVisual);
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
            var roiSettings=state.MeasurementRoi with { };
            lease=image.AcquireImageFrame(); if(lease==null)return;
            state.Busy=true;
            var snapshot=lease;
            var results=await Task.Run(()=>BmwSfrAnalyzer.Analyze(snapshot.Image,regions,options,roiSettings));
            state.ShowOverlays(image,scope,results);
            OpenWindow(image,state,scope,results,null,null,snapshot); lease=null;
        }
        catch(Exception ex) { MessageBox.Show(ex.Message,"BMW 四边 SFR",MessageBoxButton.OK,MessageBoxImage.Error); }
        finally { state.Busy=false; lease?.Dispose(); }
    }
    internal static void OpenWindow(ImageProcessingContext image,BmwDrawingAnalysisState state,ImageSelectionScope scope,
        IReadOnlyList<BmwTargetAnalysis> results,string? targetId,BmwEdgeId? edgeId,ImageFrameLease? suppliedLease=null)
    {
        var lease=suppliedLease??image.AcquireImageFrame(); if(lease==null)return;
        try
        {
            var window=new BmwSfrResultWindow(lease,results,state.Options)
                { Owner=Application.Current.GetActiveWindow(),DisplayChannel=state.DisplayChannel,DisplaySettings=state.DisplaySettings,MeasurementRoi=state.MeasurementRoi with { } };
            window.DisplayUpdated+=(updated,next,channel)=>
            {
                state.Options=next with { }; state.DisplayChannel=channel; state.DisplaySettings=window.DisplaySettings;
                state.MeasurementRoi=window.MeasurementRoi with { };
                state.ShowOverlays(image,scope,updated);
            };
            state.TrackWindow(window,results);
            if(targetId!=null && edgeId.HasValue)window.SelectEdge(targetId,edgeId.Value);
            window.Show();
        }
        catch { if(suppliedLease==null)lease.Dispose(); throw; }
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
