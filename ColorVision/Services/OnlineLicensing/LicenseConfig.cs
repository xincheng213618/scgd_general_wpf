using ColorVision.Common.MVVM;

namespace ColorVision.Services.OnlineLicensing
{
    public class LicenseConfig : ViewModelBase
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Tag { get => _Tag; set { _Tag = value; NotifyPropertyChanged(); } }
        private string _Tag;
        public string Sn { get => _Sn; set { _Sn = value; NotifyPropertyChanged(); } }
        private string _Sn;

        public string ActivationCode { get => _ActivationCode; set { _ActivationCode = value; NotifyPropertyChanged(); } }
        private string _ActivationCode;

        public bool IsCanImport { get => _IsCanImport; set { _IsCanImport = value; NotifyPropertyChanged(); } }
        private bool _IsCanImport = true;

        public object? Value { set; get; }
    }
}
