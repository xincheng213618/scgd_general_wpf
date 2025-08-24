using ColorVision.Common.MVVM;
using System.Windows.Media;

namespace ColorVision.UI.Views
{

    public enum ViewType
    {
        Hidden,
        View,
        Window,
    }
    public delegate void ViewIndexChangedHandler(int oindex, int index);
    public delegate void ViewMaxChangedHandler(int max);

    public class View : ViewModelBase
    {
        public event ViewIndexChangedHandler? ViewIndexChangedEvent;
        public void ClearViewIndexChangedSubscribers() => ViewIndexChangedEvent = null;

        public ViewGridManager ViewGridManager { get; set; }

        public int ViewIndex { get =>   _ViewIndex; set {
                if (_ViewIndex == value)
                    return;
                PreViewIndex = _ViewIndex;
                ViewIndexChangedEvent?.Invoke(_ViewIndex, value); _ViewIndex = value; OnPropertyChanged(); } }

        private int _ViewIndex = -1;
        public ViewType ViewType
        {
            get
            {
                if (ViewIndex > -1)
                    return ViewType.View;
                if (ViewIndex == -2)
                    return ViewType.Window;
                return ViewType.Hidden;
            }
        }

        public int PreViewIndex { get => _PreViewIndex; private set { _PreViewIndex = value;   } }
        private int _PreViewIndex = -1;

        public ImageSource Icon { get => _Icon; set { _Icon = value; OnPropertyChanged(); } }
        private ImageSource _Icon;

        public string Title { get => _Title; set { _Title = value; OnPropertyChanged(); }}
        private string _Title = string.Empty;
    }
}
