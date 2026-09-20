using ColorVision.Core;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.EditorTools.Algorithms.Calculate.SFR;

// Selection proxies stay out of DrawingVisualLists: an inner measurement is not a new BMW search region.
internal sealed class BmwSfrEdgeSelectionVisual : DrawingVisual, ISelectVisual
{
    private readonly BmwSfrRoiInteraction _owner;
    internal BmwSfrEdgeSelectionVisual(BmwSfrRoiInteraction owner, BmwEdgeId edge) { _owner=owner; Edge=edge; }
    internal BmwEdgeId Edge { get; }
    internal BmwSfrRoiInteraction Owner => _owner;
    public Rect GetRect() => _owner.GetBounds(Edge);
    public void SetRect(Rect rect) => _owner.ChangeBounds(Edge,rect);
}

internal sealed class BmwSfrRoiInteraction : IDisposable
{
    private readonly ImageProcessingContext _image;
    private readonly DrawEditorContext _draw;
    private readonly ImageSelectionScope _scope;
    private readonly Func<SfrAnalysisOptions> _options;
    private readonly Action<BmwTargetAnalysis> _updated;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<BmwEdgeId> _pending=[];
    private BmwTargetAnalysis _target;
    private int _generation;
    private bool _disposed, _analyzing;
    internal IReadOnlyList<BmwSfrEdgeSelectionVisual> Handles { get; }
    internal BmwTargetAnalysis Target => _target;

    internal BmwSfrRoiInteraction(ImageProcessingContext image, DrawEditorContext draw, ImageSelectionScope scope,
        BmwTargetAnalysis target, Func<SfrAnalysisOptions> options, Action<BmwTargetAnalysis> updated)
    {
        _image=image; _draw=draw; _scope=scope; _target=target; _options=options; _updated=updated;
        Handles=target.Edges.Where(e=>e.Roi.Width>0&&e.Roi.Height>0).Select(e=>new BmwSfrEdgeSelectionVisual(this,e.Id)).ToArray();
        _timer=new DispatcherTimer(TimeSpan.FromMilliseconds(150),DispatcherPriority.Background,OnTimer,draw.DrawCanvas.Dispatcher);
        _timer.Stop();
    }

    internal void UpdateTarget(BmwTargetAnalysis target)
    {
        if (ReferenceEquals(_target,target)) return;
        _generation++; _pending.Clear(); _timer.Stop(); _target=target;
        if (_draw.SelectionVisual?.PrimarySelectedVisual is BmwSfrEdgeSelectionVisual selected && ReferenceEquals(selected.Owner,this))
            _draw.SelectionVisual.Render();
    }

    internal Rect GetBounds(BmwEdgeId edge)
    {
        var r=_target.Edges.First(e=>e.Id==edge).Roi;
        return new Rect(r.X*96d/_scope.DpiX,r.Y*96d/_scope.DpiY,r.Width*96d/_scope.DpiX,r.Height*96d/_scope.DpiY);
    }

    internal bool ChangeBounds(BmwEdgeId edgeId, Rect bounds)
    {
        if (_disposed || !TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope)) return false;
        if (bounds.IsEmpty || !new[]{bounds.X,bounds.Y,bounds.Width,bounds.Height}.All(double.IsFinite) || bounds.Width<=0 || bounds.Height<=0) return false;
        var parent=_target.SearchRoi;
        int width=(int)Math.Clamp(Math.Round(bounds.Width*_scope.DpiX/96),1,parent.Width);
        int height=(int)Math.Clamp(Math.Round(bounds.Height*_scope.DpiY/96),1,parent.Height);
        int x=(int)Math.Clamp(Math.Round(bounds.X*_scope.DpiX/96),parent.X,(double)parent.X+parent.Width-width);
        int y=(int)Math.Clamp(Math.Round(bounds.Y*_scope.DpiY/96),parent.Y,(double)parent.Y+parent.Height-height);
        var roi=new RoiRect(x,y,width,height);
        var edge=_target.Edges.First(e=>e.Id==edgeId);
        if (edge.Roi.Equals(roi)) return false;
        _generation++; _pending.Add(edgeId);
        _target=_target with { Edges=_target.Edges.Select(e=>e.Id==edgeId?e with { Roi=roi,Analysis=null,Valid=false,Reason="roi_changed" }:e).ToArray() };
        _updated(_target);
        _timer.Stop(); _timer.Start();
        return true;
    }

    private async void OnTimer(object? sender,EventArgs e)
    {
        if (_draw.DrawCanvas.IsMouseCaptured || _analyzing) return;
        _timer.Stop();
        await FlushPendingAsync();
    }

    internal async Task FlushPendingAsync()
    {
        if (_disposed || _analyzing || _pending.Count==0) return;
        _timer.Stop();
        if (!TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope)) { Dispose(); return; }
        using var lease=_image.AcquireImageFrame();
        if (lease==null) return;
        int generation=_generation;
        var target=_target;
        var pending=_pending.ToArray();
        var options=_options() with { };
        _analyzing=true;
        try
        {
            var edges=await Task.Run(()=>target.Edges.Select(edge=>pending.Contains(edge.Id)?BmwSfrResultWindow.AnalyzeEdge(lease.Image,edge,options):edge).ToArray());
            if (_disposed || generation!=_generation || !TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope)) return;
            _pending.Clear();
            _target=target with { Edges=edges };
            _updated(_target);
        }
        catch (Exception ex)
        {
            if (_disposed || generation!=_generation || !TransientRoiSelectionSession.IsSourceScopeCurrent(_image,_scope)) return;
            _pending.Clear();
            _target=target with { Edges=target.Edges.Select(edge=>pending.Contains(edge.Id)?edge with { Analysis=null,Valid=false,Reason=ex.Message }:edge).ToArray() };
            _updated(_target);
        }
        finally
        {
            _analyzing=false;
            if (!_disposed && _pending.Count>0) _timer.Start();
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed=true; _generation++; _pending.Clear(); _timer.Stop(); _timer.Tick-=OnTimer;
        if (_draw.SelectionVisual?.SelectVisuals.OfType<BmwSfrEdgeSelectionVisual>().Any(v=>ReferenceEquals(v.Owner,this))==true)
            _draw.SelectionVisual.ClearRender();
    }
}
