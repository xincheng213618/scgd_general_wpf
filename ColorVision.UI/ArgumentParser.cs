using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.UI
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
        public static ArgumentParser GetInstance() { lock (_locker) { return _instance ??= new ArgumentParser(); } }

        private readonly List<Argument> _arguments = new List<Argument>();
        private readonly Dictionary<string, string> _parsedArguments = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public string[] CommandLineArgs { get; set; }
        public ArgumentParser()
        {

        }
        public void AddArgument(string name, bool isFlag = false, params string[] aliases)
        {
            _arguments.Add(new Argument(name, isFlag, aliases));
        }
        public void Parse() => Parse(CommandLineArgs);
        public void Parse(string[] args)
        {
            var aliasMap = BuildAliasMap();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-"))
                {
                    string key = args[i].TrimStart('-').ToLower();
                    if (aliasMap.ContainsKey(key))
                    {
                        key = aliasMap[key];
                    }

                    if (_arguments.Exists(arg => arg.Name.Equals(key, StringComparison.OrdinalIgnoreCase)))
                    {
                        var argument = _arguments.Find(arg => arg.Name.Equals(key, StringComparison.OrdinalIgnoreCase));
                        string value = argument.IsFlag ? "true" : (i + 1 < args.Length && !args[i + 1].StartsWith("-")) ? args[i + 1] : null;

                        if (value != null)
                        {
                            _parsedArguments[key] = value;
                            if (!argument.IsFlag)
                            {
                                i++; // Skip the next argument if it's a value
                            }
                        }
                    }
                }
            }
        }
        public bool GetFlag(string name)
        {
            return _parsedArguments.ContainsKey(name) && _parsedArguments[name].Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public string GetValue(string name)
        {
            return _parsedArguments.ContainsKey(name) ? _parsedArguments[name] : null;
        }

        private Dictionary<string, string> BuildAliasMap()
        {
            var aliasMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var argument in _arguments)
            {
                aliasMap[argument.Name.ToLower()] = argument.Name.ToLower();
                foreach (var alias in argument.Aliases)
                {
                    aliasMap[alias.ToLower()] = argument.Name.ToLower();
                }
            }

            return aliasMap;
        }





    }
}
