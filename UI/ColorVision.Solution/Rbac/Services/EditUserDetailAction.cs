using ColorVision.Rbac.Dtos;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using System.Windows;

namespace ColorVision.Rbac.Services
{
    public class EditUserDetailAction
    {
        private readonly IUserService _userService;
        public EditUserDetailAction(IUserService userService)
        {
            _userService = userService;
        }

        // 将你的 Edit 改成异步。若在命令里可用 async void，但建议上层继续 Task 化
        public async Task EditAsync()
        {
            // 检查是否已登录
            var config = RbacManagerConfig.Instance;
            if (config.LoginResult == null || config.LoginResult.UserDetail == null)
            {
                MessageBox.Show("请先登录后再编辑用户信息。", "未登录", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 原始对象（登录后的缓存）
            var original = config.LoginResult.UserDetail;

            // 做一个可编辑副本，防止取消时污染原对象
            var editable = new UserDetailDto
            {
                UserId = original.UserId,
                Email = original.Email,
                Phone = original.Phone,
                Address = original.Address,
                Company = original.Company,
                Department = original.Department,
                Position = original.Position,
                Remark = original.Remark,
                UserImage = original.UserImage,
                PermissionMode = original.PermissionMode,
                CreatedAt = original.CreatedAt,
                UpdatedAt = original.UpdatedAt
            };

            var dialog = new PropertyEditorWindow(editable)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            dialog.ShowDialog();
            try
            {
                // 用原对象的 UpdatedAt 作为并发期望值
                await _userService.UpdateUserDetailAsync(editable, expectedUpdatedAt: original.UpdatedAt);

                // 保存成功：用 editable 覆盖原对象的值
                original.Email = editable.Email;
                original.Phone = editable.Phone;
                original.Address = editable.Address;
                original.Company = editable.Company;
                original.Department = editable.Department;
                original.Position = editable.Position;
                original.Remark = editable.Remark;
                original.UserImage = editable.UserImage;
                original.PermissionMode = editable.PermissionMode;
                original.UpdatedAt = editable.UpdatedAt;

                // 根据保存后的值更新运行时权限
                Authorization.Instance.PermissionMode = original.PermissionMode;
            }
            catch (InvalidOperationException ex)
            {
                MessageBox.Show(ex.Message, "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存用户详情时出现异常：" + ex.Message, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

}
