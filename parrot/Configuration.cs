using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace Parrot
{
    internal sealed class RuntimeConfig
    {
        public string CommandDatabaseDirectory { get; set; }

        public string CorrectionsDictionaryPath { get; set; }

        public string CultureCode { get; set; }

        private RuntimeConfig()
        {
            // NOTE Private ctor to enforce factory pattern
        }

        public static RuntimeConfig FromCommandLine(string[] args)
        {
            if (args == null || args.Length < 2)
                return null;
            
            return new RuntimeConfig
            {
                CultureCode = "en-US",
                CommandDatabaseDirectory = args[0],
                CorrectionsDictionaryPath = args[1]
            };
        }

        public static void PrintHelp()
        {
            Console.WriteLine("parrot <Database Directory> <Corrections.json>");
        }
    }

    public sealed class CommandDatabaseConfig
    {
        public bool Enabled { get; set; }

        public List<CommandConfig> Commands { get; set; }

        private CommandDatabaseConfig()
        {
            Commands = new List<CommandConfig>();
        }

        public static CommandDatabaseConfig FromDirectory(string directory)
        {
            if (directory == null)
                throw new ArgumentNullException("directory");

            var config = new CommandDatabaseConfig();

            var di = new DirectoryInfo(directory);
            var files = di.GetFiles("*.json");
            foreach (var file in files)
            {
                var fragmentContents = File.ReadAllText(file.FullName);
                var fragmentConfig = JsonConvert.DeserializeObject<CommandDatabaseConfig>(fragmentContents);

                config.MergeFrom(fragmentConfig);
            }

            config.Commands.Sort((l, r) =>
            {
                int lLen = l.Pattern.Length - (Math.Max(l.Pattern.LastIndexOf('}'), 0) - Math.Max(l.Pattern.IndexOf('{'), 0));
                int rLen = r.Pattern.Length - (Math.Max(r.Pattern.LastIndexOf('}'), 0) - Math.Max(r.Pattern.IndexOf('{'), 0));

                return rLen - lLen;
            });

            config.Validate();

            return config;
        }

        private void MergeFrom(CommandDatabaseConfig source)
        {
            if (!source.Enabled)
                return;
            
            Commands.AddRange(source.Commands);
        }

        private void Validate()
        {
            
        }
    }

    public sealed class CommandConfig
    {
        public string Pattern { get; set; }

        public bool TerminateDictation { get; set; }

        public CommandContext Context { get; set; }

        public List<ActionConfig> Actions { get; set; }
    }

    public sealed class ActionConfig : Dictionary<string, string>
    {
        public string Type
        {
            get { return this["Type"]; }
        }
    }

    public sealed class CommandContext : Dictionary<string, string>
    {
        public string Type
        {
            get { return this["Type"]; }
        }
    }
}
