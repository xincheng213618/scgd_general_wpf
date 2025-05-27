namespace ColorVision.UI.Shell
{
    public class Argument
    {
        public string LongName { get; }
        public string ShortName { get; }
        public bool IsOption { get; }

        public string Help { get; set; }

        public Argument() { }
        public Argument(string shortName,string longName, bool isOption, string help)
        {
            LongName = longName;
            IsOption = isOption;
            ShortName = shortName;
            Help = help;
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

        public string Description { get; set; }

        public ArgumentParser(string description ="")
        {
            AddArgument("input", false, "i");
            AddArgument("version", false, "i");
            Description = description;
        }
        public void AddArgument(string name, bool isFlag = false, string aliases ="")
        {
            _arguments.Add(new Argument(aliases,name,isFlag,""));
        }

        public void AddArgument(Argument argument)
        {
            _arguments.Add(argument);
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

                    if (_arguments.Exists(arg => arg.LongName.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        Argument argument = _arguments.Find(arg => arg.LongName.Equals(key, StringComparison.OrdinalIgnoreCase));
                        if (argument != null)
                        {
                            string value1 = argument.IsOption ? "true" : i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.CurrentCulture) ? args[i + 1] : null;
                            if (value1 != null)
                            {
                                _parsedArguments[key] = value1;
                                if (!argument.IsOption)
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
                aliasMap[argument.LongName.ToLowerInvariant()] = argument.LongName;
                aliasMap[argument.ShortName.ToLowerInvariant()] = argument.LongName;
            }

            return aliasMap;
        }





    }
}
