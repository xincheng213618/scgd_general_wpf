namespace ColorVision.UI.Shell
{
    public sealed record ArgumentParseResult(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlyList<string> PositionalArguments);

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
            AddArgument("version", false, "v");
            AddArgument("project", false, "p");
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
            foreach (KeyValuePair<string, string> item in ParseSnapshot(args).Values)
                _parsedArguments[item.Key] = item.Value;
        }

        /// <summary>
        /// Parses an independent argument snapshot without changing the values
        /// retained for the current application instance.
        /// </summary>
        public IReadOnlyDictionary<string, string> ParseValues(string[] args) => ParseSnapshot(args).Values;

        public ArgumentParseResult ParseSnapshot(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);
            var parsedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var positionalArguments = new List<string>();
            var aliasMap = BuildAliasMap();

            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-", StringComparison.CurrentCulture))
                {
                    if (!string.IsNullOrWhiteSpace(args[i]))
                        positionalArguments.Add(args[i]);
                    continue;
                }

                string key = args[i].TrimStart('-').ToLower(System.Globalization.CultureInfo.CurrentCulture);
                if (aliasMap.TryGetValue(key, out string? canonicalName))
                    key = canonicalName;

                Argument? argument = _arguments.Find(candidate =>
                    candidate.LongName.Equals(key, StringComparison.OrdinalIgnoreCase));
                if (argument == null)
                    continue;

                string? argumentValue = argument.IsOption
                    ? "true"
                    : i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.CurrentCulture)
                        ? args[i + 1]
                        : null;
                if (argumentValue == null)
                    continue;

                parsedArguments[key] = argumentValue;
                if (!argument.IsOption)
                    i++;
            }

            if (!parsedArguments.ContainsKey("input") && positionalArguments.Count > 0)
                parsedArguments["input"] = positionalArguments[0];

            return new ArgumentParseResult(parsedArguments, positionalArguments);
        }
        public bool GetFlag(string name) => _parsedArguments.TryGetValue(name, out var value) && value.Equals("true", StringComparison.OrdinalIgnoreCase);

        public string? GetValue(string name) => _parsedArguments.TryGetValue(name, out var value) ? value : null;
        public void SetValue(string name,string value)
        {
            _parsedArguments[name] = value;
        }


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
