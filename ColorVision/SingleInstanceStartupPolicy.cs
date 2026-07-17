namespace ColorVision
{
    internal enum SingleInstanceStartupAction
    {
        StartCurrentInstance,
        ActivateExistingInstance,
    }

    internal static class SingleInstanceStartupPolicy
    {
        public static SingleInstanceStartupAction Decide(
            bool ownsMutex,
            bool isDebuggerAttached,
            bool allowMultipleInstances)
        {
            return ownsMutex || isDebuggerAttached || allowMultipleInstances
                ? SingleInstanceStartupAction.StartCurrentInstance
                : SingleInstanceStartupAction.ActivateExistingInstance;
        }
    }
}
