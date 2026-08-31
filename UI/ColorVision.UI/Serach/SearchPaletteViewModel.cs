using ColorVision.Common.MVVM;
using log4net;
using System.Globalization;

namespace ColorVision.UI.Serach;

internal sealed record SearchCategoryOption(string? Key, string Label);

internal sealed class SearchPaletteEntry
{
    internal SearchPaletteEntry(SearchResultItem result, bool available)
    {
        Result = result;
        IsAvailable = available;
    }

    internal SearchResultItem Result { get; }
    public string Title => Result.Title;
    public string Description => Result.Description;
    public string Category => Result.Category;
    public string ShortcutText => Result.ShortcutText;
    public bool HasShortcut => !string.IsNullOrEmpty(ShortcutText);
    public bool IsAvailable { get; }
    public string Details => IsAvailable ? $"{Category} · {Description}" : SearchPaletteText.Get("Unavailable");
    public string Glyph => Result.CategoryKey switch
    {
        "Settings" => "\uE713", "Templates" => "\uE8A5", "FlowNodes" => "\uE8F1",
        "Tools" => "\uE90F", "External" => "\uE774", _ => "\uE756"
    };
}

internal sealed class SearchPaletteViewModel : ViewModelBase
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(SearchPaletteViewModel));
    private readonly Func<string, string?, CancellationToken, Task<SearchQueryResult>> _query;
    private readonly Func<SearchResultItem, bool> _canExecute;
    private readonly TimeSpan _debounce;
    private CancellationTokenSource? _pending;
    private long _version;
    private bool _ready;
    private string _searchText = string.Empty;
    private SearchCategoryOption _category;
    private SearchPaletteEntry? _selected;

    internal SearchPaletteViewModel(Func<string, string?, CancellationToken, Task<SearchQueryResult>> query,
        Func<SearchResultItem, bool> canExecute, TimeSpan? debounce = null)
    {
        _query = query;
        _canExecute = canExecute;
        _debounce = debounce ?? TimeSpan.FromMilliseconds(120);
        Categories = new[] { new SearchCategoryOption(null, SearchPaletteText.AllCategories) }
            .Concat(new[] { "Commands", "Settings", "Templates", "FlowNodes", "Tools", "External" }
                .Select(key => new SearchCategoryOption(key, SearchPaletteText.Get("Category" + key)))).ToArray();
        _category = Categories[0];
    }

    public IReadOnlyList<SearchCategoryOption> Categories { get; }
    public IReadOnlyList<SearchPaletteEntry> Results { get; private set; } = [];
    public bool IsOpen { get; private set; }
    public bool IsSearching { get; private set; }
    public bool IsEmpty => !IsSearching && Results.Count == 0;
    public bool HasSearch => SearchText.Length > 0;
    public bool HasStatus => !string.IsNullOrEmpty(Status);
    public string Summary { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    internal Task PendingSearch { get; private set; } = Task.CompletedTask;

    public string SearchText
    {
        get => _searchText;
        set
        {
            value ??= string.Empty;
            if (value == _searchText) return;
            _searchText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSearch));
            Refresh();
        }
    }

    public SearchCategoryOption Category
    {
        get => _category;
        set
        {
            if (value == null || value == _category) return;
            _category = value;
            OnPropertyChanged();
            Refresh();
        }
    }

    public SearchPaletteEntry? Selected
    {
        get => _selected;
        set { if (_selected != value) { _selected = value; OnPropertyChanged(); } }
    }

    internal void Open()
    {
        IsOpen = true;
        _searchText = string.Empty;
        _category = Categories[0];
        OnPropertyChanged(nameof(SearchText));
        OnPropertyChanged(nameof(HasSearch));
        OnPropertyChanged(nameof(Category));
        Refresh(immediate: true);
    }

    internal void Close()
    {
        IsOpen = false;
        Invalidate();
        Results = [];
        Selected = null;
        IsSearching = false;
        NotifyResults();
    }

    internal void Refresh(bool immediate = false)
    {
        if (!IsOpen) return;
        Invalidate();
        // Clear acceptance immediately, not when debounce expires. Old rows must never execute.
        Results = [];
        Selected = null;
        IsSearching = true;
        Status = string.Empty;
        Summary = SearchPaletteText.Get("Searching");
        NotifyResults();
        var cancellation = new CancellationTokenSource();
        _pending = cancellation;
        PendingSearch = QueryAsync(SearchText, Category.Key, _version, cancellation, immediate);
    }

    private async Task QueryAsync(string query, string? category, long version, CancellationTokenSource cancellation, bool immediate)
    {
        try
        {
            if (!immediate && _debounce > TimeSpan.Zero) await Task.Delay(_debounce, cancellation.Token);
            // Yield so even synchronous legacy providers run after the loading state can render.
            await Task.Yield();
            cancellation.Token.ThrowIfCancellationRequested();
            SearchQueryResult response = await _query(query, category, cancellation.Token);
            if (!IsOpen || version != _version || cancellation.IsCancellationRequested) return;
            Results = response.Items.Select(item => new SearchPaletteEntry(item, _canExecute(item))).ToArray();
            Selected = Results.FirstOrDefault(item => item.IsAvailable) ?? Results.FirstOrDefault();
            Summary = string.IsNullOrWhiteSpace(query) ? SearchPaletteText.Get("RecentAndCommon")
                : string.Format(CultureInfo.CurrentCulture, SearchPaletteText.Get(response.IsTruncated ? "TruncatedCount" : "ResultCount"), Results.Count);
            Status = response.FailedSources.Count > 0 ? SearchPaletteText.Get("PartialFailure") : string.Empty;
            _ready = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            if (!IsOpen || version != _version) return;
            Log.Warn("Search palette query failed.", exception);
            Results = [];
            Selected = null;
            Status = SearchPaletteText.Get("SearchFailed");
            Summary = string.Empty;
        }
        finally
        {
            if (IsOpen && version == _version)
            {
                IsSearching = false;
                NotifyResults();
            }
            if (ReferenceEquals(_pending, cancellation)) _pending = null;
            cancellation.Dispose();
        }
    }

    internal bool TryGetSelection(out SearchPaletteEntry? selected)
    {
        selected = _ready && IsOpen && Selected != null && Results.Contains(Selected) ? Selected : null;
        return selected != null;
    }

    internal void MoveSelection(int direction)
    {
        if (!_ready || Results.Count == 0) return;
        int index = Selected == null ? -1 : Results.ToList().IndexOf(Selected);
        Selected = Results[Math.Clamp(index + direction, 0, Results.Count - 1)];
    }

    internal void SetStatus(string message)
    {
        Status = message;
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasStatus));
    }

    private void Invalidate()
    {
        _ready = false;
        _version++;
        _pending?.Cancel();
        _pending = null;
    }

    private void NotifyResults()
    {
        OnPropertyChanged(nameof(Results));
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(HasStatus));
    }
}
