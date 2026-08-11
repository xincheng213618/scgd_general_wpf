using System;
using System.Windows;

namespace ColorVision.Themes.Controls.Uploads
{
    /// <summary>
    /// UploadMsg.xaml 的交互逻辑
    /// </summary>
    public partial class UploadMsg : Window
    {
        private IUploadMsg? _subscribedUploadMsg;

        public IUploadMsg IUploadMsg1 { get; set; }

        public UploadMsg(IUploadMsg iUploadMsg)
        {
            IUploadMsg1 = iUploadMsg;
            InitializeComponent();
            this.ApplyCaption();
        }

        private void Window_Initialized(object sender, EventArgs e)
        {
            DataContext = IUploadMsg1;
            _subscribedUploadMsg = IUploadMsg1;
            _subscribedUploadMsg.UploadClosed += UploadMsg_UploadClosed;
        }

        private void UploadMsg_UploadClosed(object? sender, EventArgs e) => Close();

        protected override void OnClosed(EventArgs e)
        {
            if (_subscribedUploadMsg is not null)
            {
                _subscribedUploadMsg.UploadClosed -= UploadMsg_UploadClosed;
                _subscribedUploadMsg = null;
            }

            DataContext = null;
            base.OnClosed(e);
        }
    }
}
