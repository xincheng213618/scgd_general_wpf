namespace ColorVision
{
    internal enum SingleInstanceStartupAction
    {
        StartCurrentInstance,
        ReplaceEarlierInstances,
    }

    internal static class SingleInstanceStartupPolicy
    {
        public static SingleInstanceStartupAction Decide(
            bool isDebuggerAttached,
            bool allowMultipleInstances)
        {
            return isDebuggerAttached || allowMultipleInstances
                ? SingleInstanceStartupAction.StartCurrentInstance
                : SingleInstanceStartupAction.ReplaceEarlierInstances;
        }
    }
}
