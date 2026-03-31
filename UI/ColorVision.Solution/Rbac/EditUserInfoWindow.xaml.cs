using ColorVision.Rbac.Dtos;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Rbac
{
    /// <summary>
    /// EditUserInfoWindow.xaml 的交互逻辑
    /// </summary>
    public partial class EditUserInfoWindow : Window
    {
        private readonly UserDetailDto _userDetail;

        public EditUserInfoWindow(UserDetailDto userDetail)
        {
            _userDetail = userDetail;
            InitializeComponent();
            LoadUserDetail();
        }

        private void LoadUserDetail()
        {
            TxtEmail.Text = _userDetail.Email ?? string.Empty;
            TxtPhone.Text = _userDetail.Phone ?? string.Empty;
            TxtCompany.Text = _userDetail.Company ?? string.Empty;
            TxtDepartment.Text = _userDetail.Department ?? string.Empty;
            TxtPosition.Text = _userDetail.Position ?? string.Empty;
            TxtAddress.Text = _userDetail.Address ?? string.Empty;
            TxtRemark.Text = _userDetail.Remark ?? string.Empty;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            _userDetail.Email = TxtEmail.Text.Trim();
            _userDetail.Phone = TxtPhone.Text.Trim();
            _userDetail.Company = TxtCompany.Text.Trim();
            _userDetail.Department = TxtDepartment.Text.Trim();
            _userDetail.Position = TxtPosition.Text.Trim();
            _userDetail.Address = TxtAddress.Text.Trim();
            _userDetail.Remark = TxtRemark.Text.Trim();

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
