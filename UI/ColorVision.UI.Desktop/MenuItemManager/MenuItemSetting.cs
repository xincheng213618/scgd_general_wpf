using ColorVision.Common.MVVM;
using System.ComponentModel;

namespace ColorVision.UI.Desktop.MenuItemManager
{
    public class MenuItemSetting : ViewModelBase
    {
        public string GuidId { get => _guidId; set => SetProperty(ref _guidId, value); }
        private string _guidId = string.Empty;

        public string? OwnerGuid { get => _ownerGuid; set => SetProperty(ref _ownerGuid, value); }
        private string? _ownerGuid;

        [Browsable(false)]
        public string? Header { get => _header; set => SetProperty(ref _header, value); }
        private string? _header;

        [Browsable(false)]
        public int DefaultOrder { get => _defaultOrder; set => SetProperty(ref _defaultOrder, value); }
        private int _defaultOrder;

        [DisplayName("Visible")]
        public bool IsVisible { get => _isVisible; set => SetProperty(ref _isVisible, value); }
        private bool _isVisible = true;

        [DisplayName("Order")]
        public int? OrderOverride { get => _orderOverride; set => SetProperty(ref _orderOverride, value); }
        private int? _orderOverride;

        [DisplayName("Hotkey")]
        public string? HotkeyOverride { get => _hotkeyOverride; set => SetProperty(ref _hotkeyOverride, value); }
        private string? _hotkeyOverride;

        [DisplayName("OwnerGuid Override")]
        public string? OwnerGuidOverride { get => _ownerGuidOverride; set => SetProperty(ref _ownerGuidOverride, value); }
        private string? _ownerGuidOverride;
    }
}
