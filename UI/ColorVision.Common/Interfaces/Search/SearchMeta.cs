using System;
using System.Windows.Input;
using System.Collections.Generic;

namespace ColorVision.UI
{
    public class SearchMeta: ISearch, ISearchMetadata
    {
        public SearchType Type { get; set; } = SearchType.Menu;
        public string? GuidId { get; set; }
        public string? Header { get; set; }
        public object? Icon { get; set; }
        public ICommand? Command { get; set; }
        public string? Description { get; set; }
        public string? CategoryKey { get; set; }
        public string? Category { get; set; }
        public IReadOnlyList<string> Aliases { get; set; } = Array.Empty<string>();
        public string? ActionId { get; set; }
    }

}
