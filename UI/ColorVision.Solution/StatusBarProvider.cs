using ColorVision.UI;

namespace ColorVision.Solution
{
    public class StatusBarProvider :  IStatusBarProvider
    {
        public IEnumerable<StatusBarMeta> GetStatusBarIconMetadata()
        {
            Action action = new Action(() => {  });
             
            return new List<StatusBarMeta>
            {
                new StatusBarMeta()
                {
                    Name = "IsLackWarning",
                    Description = "IsLackWarning",
                    Order =4,
                    BindingName = nameof(SolutionSetting.IsLackWarning),
                    ButtonStyleName ="ButtonDrawingImageHardDisk",
                    Source = SolutionSetting.Instance,
                    Action =action
                }
            };
        }
    }
}