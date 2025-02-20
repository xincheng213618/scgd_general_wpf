namespace ColorVision.UI.Shell
{
    public class Argument
    {
        public string Name { get; }
        public List<string> Aliases { get; }
        public bool IsFlag { get; }

        public Argument(string name, bool isFlag, params string[] aliases)
        {
            Name = name;
            IsFlag = isFlag;
            Aliases = new List<string>(aliases);
        }
    }

    public class ArgumentParser
    {
        private static ArgumentParser _instance;
        private static readonly object _locker = new();
        public static ArgumentParser GetInstance()  { lock (_locker) { return _instance ??= new ArgumentParser(); } }

        private readonly List<Argument> _arguments = new List<Argument>();
        private readonly Dictionary<string, string> _parsedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string[] CommandLineArgs { get; set; } = Array.Empty<string>();
        public ArgumentParser()
        {
            AddArgument("input", false, "i");
        }
        public void AddArgument(string name, bool isFlag = false, params string[] aliases)
        {
            _arguments.Add(new Argument(name, isFlag, aliases));
        }

        public void Parse() => Parse(CommandLineArgs);
        public void Parse(string[] args)
        {
            CommandLineArgs = args;
            if (args.Length == 1 && !args[0].StartsWith("-" , StringComparison.CurrentCulture))
            {
                // Handle the case where only a file path is provided
                _parsedArguments["input"] = args[0];
                return;
            }

            var aliasMap = BuildAliasMap();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-", StringComparison.CurrentCulture))
                {
                    string key = args[i].TrimStart('-').ToLower(System.Globalization.CultureInfo.CurrentCulture);
                    if (aliasMap.TryGetValue(key, out string? value))
                    {
                        key = value;
                    }

                    if (_arguments.Exists(arg => arg.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        Argument argument = _arguments.Find(arg => arg.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                        if (argument != null)
                        {
                            string value1 = argument.IsFlag ? "true" : i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.CurrentCulture) ? args[i + 1] : null;
                            if (value1 != null)
                            {
                                _parsedArguments[key] = value1;
                                if (!argument.IsFlag)
                                {
                                    i++; // Skip the next argument if it's a value
                                }
                            }
                        }
                    }
                }
            }
        }
        public bool GetFlag(string name) => _parsedArguments.TryGetValue(name, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);

        public string? GetValue(string name) => _parsedArguments.TryGetValue(name, out var value) ? value : null;

        private Dictionary<string, string> BuildAliasMap()
        {
            var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var argument in _arguments)
            {
                aliasMap[argument.Name.ToLowerInvariant()] = argument.Name;
                foreach (var alias in argument.Aliases)
                {
                    aliasMap[alias.ToLowerInvariant()] = argument.Name;
                }
            }

            return aliasMap;
        }





    }
}
