using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ColorVision.Solution.Explorer;

/// <summary>Tracks the attached paths across yields during root directory population.</summary>
internal sealed class SolutionChildPathIndex : IDisposable
{
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<SolutionNode> _observedNodes = new();
    private readonly PropertyChangedEventHandler _onChildPropertyChanged;
    private ObservableCollection<SolutionNode>? _children;
    private bool _dirty;

    internal SolutionChildPathIndex() => _onChildPropertyChanged = OnChildPropertyChanged;

    internal bool Contains(ObservableCollection<SolutionNode> children, string path)
    {
        if (_dirty || !ReferenceEquals(_children, children))
        {
            StopTracking();
            _children = children;
            _children.CollectionChanged += OnChildrenChanged;
            foreach (SolutionNode child in children) Observe(child);
            _dirty = false;
        }
        return _paths.Contains(path);
    }

    private void Observe(SolutionNode node)
    {
        _observedNodes.Add(node);
        node.PropertyChanged += _onChildPropertyChanged;
        if (!string.IsNullOrEmpty(node.FullPath)) _paths.Add(node.FullPath);
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (_dirty || e.Action == NotifyCollectionChangedAction.Move) return;
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (SolutionNode child in e.NewItems) Observe(child);
        }
        else
            _dirty = true;
    }

    private void OnChildPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) || e.PropertyName == nameof(SolutionNode.FullPath))
            _dirty = true;
    }

    public void Dispose() => StopTracking();

    private void StopTracking()
    {
        if (_children != null) _children.CollectionChanged -= OnChildrenChanged;
        _children = null;
        foreach (SolutionNode node in _observedNodes) node.PropertyChanged -= _onChildPropertyChanged;
        _observedNodes.Clear();
        _paths.Clear();
    }
}
