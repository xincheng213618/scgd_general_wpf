using ColorVision.Solution.Editor;
using ColorVision.UI;
using ColorVision.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision
{
    internal static class ForwardedCommandLineHandler
    {
        public static void Handle(string[] arguments)
        {
            ArgumentParser parser = ArgumentParser.GetInstance();
            ArgumentParseResult parsedArguments = parser.ParseSnapshot(arguments);
            CommandLineResourceOpenRequest request = CommandLineResourceOpenRequest.Create(parsedArguments);
            _ = ResourceOpenService.Instance.TryOpenCommandLineWithFeedbackAsync(request);

            if (!parsedArguments.Values.TryGetValue("project", out string? project))
                return;

            var launchers = new List<IFeatureLauncher>();
            foreach (Type type in AssemblyHandler.GetInstance().GetImplementationTypes(typeof(IFeatureLauncher)))
            {
                if (Activator.CreateInstance(type) is IFeatureLauncher launcher)
                    launchers.Add(launcher);
            }

            launchers.FirstOrDefault(launcher => launcher.Header == project)?.Execute();
        }
    }
}
