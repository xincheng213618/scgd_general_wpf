namespace ProjectARVRPro.Process.Black
{
    public class BlackProcessConfig : ProcessConfigBase
    {
        public bool IsUsingNing { get => _IsUsingNing; set { _IsUsingNing = value; OnPropertyChanged(); } }
        private bool _IsUsingNing ;

        public string FormatString { get => _FormatString; set { _FormatString = value; OnPropertyChanged(); } }
        private string _FormatString = "F2";

        public string Key_Center { get => _Key_Center; set { _Key_Center = value; OnPropertyChanged(); } }
        private string _Key_Center = "P_5";
    }
}
