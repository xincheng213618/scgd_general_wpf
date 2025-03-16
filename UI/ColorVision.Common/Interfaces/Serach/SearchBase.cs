using System.Windows.Input;

namespace ColorVision.UI
{
    public abstract class SearchBase : ISearch
    {
        public SearchType Type => SearchType.Menu;
        public virtual string? GuidId => GetType().Name;
        public string? Header { get;  } =string.Empty;
        public object? Icon { get;  }
        public ICommand? Command { get;  }
    }

}
