using System.Windows.Input;

namespace ColorVision.UI
{
    public interface ISearch
    {
        public SearchType Type { get; }
        public string? GuidId { get; }
        public string? Header { get; }
        public object? Icon { get; }
        public ICommand? Command { get; }
    }


}
