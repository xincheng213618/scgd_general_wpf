namespace ColorVision.UI.Configs
{
    public class StatusBarIconMetadata
    {
        public string Name { get; set; }
        /// <summary>
        /// 描述项，还是要看实现
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// 如果需要变更顺序，可以通过Order来控制
        /// </summary>
        public int Order { get; set; }

        /// <summary>
        /// Bool
        /// </summary>
        public string BindingName { get; set; }

        public string VisibilityBindingName { get; set; }

        public string ButtonStyleName { get; set; }

        public object Source { get; set; }

        public Action Action { get; set; }
    }

    public interface IStatusBarProvider
    {
        IEnumerable<StatusBarIconMetadata> GetStatusBarIconMetadata();
    }
}
