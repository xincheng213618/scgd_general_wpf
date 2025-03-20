using System;
using System.Windows.Input;

namespace ColorVision.UI
{
    public class SearchMeta: ISearch
    {
        public SearchType Type { get; set; } = SearchType.Menu;
        public string? GuidId { get; set; } = Guid.NewGuid().ToString();
        public string? Header { get; set; }
        public object? Icon { get; set; }
        public ICommand? Command { get; set; }
    }

}
