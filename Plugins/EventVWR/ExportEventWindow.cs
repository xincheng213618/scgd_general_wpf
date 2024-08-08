using ColorVision.Common.Utilities;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Menus;
using System.Windows;

namespace EventVWR
{
    public class ExportEventWindow : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "EventWindow";
        public override int Order => 1000;
        public override string Header => "EventWindow";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            new EventWindow() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
        }
    }



    public class ExportUploadWindow : MenuItemBase
    {
        public override string OwnerGuid => "Help";
        public override string GuidId => "Upload";
        public override int Order => 1000;
        public override string Header => "上传文件";

        [RequiresPermission(PermissionMode.Administrator)]
        public override void Execute()
        {
            PlatformHelper.Open("http://xc213618.ddns.me:9998");

            //string filePath = "C:\\Users\\17917\\Documents\\WXWork\\1688854819471931\\Cache\\File\\2024-01\\Calibration.zip"; // 替换为你的文件路径
            //string uploadUrl = "http://xc213618.ddns.me:9998/upload/" + Path.GetFileName(filePath);
            //new WindowUpdate(this).Show();
            //try
            //{
            //    Task.Run(() => UploadFileAsync(filePath, uploadUrl));
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine("Error uploading file: " + ex.Message);
            //}
        }

        //async Task UploadFileAsync(string filePath, string uploadUrl)
        //{
        //    using (var httpClient = new HttpClient())
        //    using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        //    {
        //        var content = new ProgressableStreamContent(fileStream, 4096, (progress, speed, timeRemaining) =>
        //        {
        //            ProgressValue = (int)progress;
        //            SpeedValue = speed;
        //            RemainingTimeValue = timeRemaining;
        //        });

        //        content.Headers.Add("Content-Type", "application/octet-stream");

        //        var response = await httpClient.PutAsync(uploadUrl, content);
        //        response.EnsureSuccessStatusCode();
        //    }
        //    MessageBox1.Show("上传成功");
        //}

    }







}
