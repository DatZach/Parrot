using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Parrot
{
    // TODO This is also kind of a god object
    public sealed class CommandEngine : ISpeechRecognitionService
    {
        public bool Enabled { get; set; }

        public string Context { get; set; }

        private string segment;
        private DateTime lastTs;

        private readonly Dictionary<string, IAction> actions;
        private readonly CommandDatabaseConfig commandDatabase;
        
        public CommandEngine()
        {
            Enabled = false;

            segment = "";
            lastTs = DateTime.MinValue;

            actions = new Dictionary<string, IAction>();
            commandDatabase = CommandDatabaseConfig.FromDirectory(Program.Config.CommandDatabaseDirectory);

            var asm = Assembly.GetExecutingAssembly();
            foreach (var type in asm.GetTypes()
                .Where(x => typeof(IAction).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract))
            {
                actions[type.Name] = (IAction)Activator.CreateInstance(type);
            }
        }

        public void Initialize()
        {
            // TODO Can do a soft-reload if I load config here instead of in ctor
        }

        public void Shutdown()
        {
            actions.Clear();
        }

        private readonly object padLock = new object();

        // TODO Clean this up
        public void OnSpeechRecognition(RecognizedSpeechEventArgs e)
        {
            lock (padLock)
            {
                switch (e.Type)
                {
                    case RecognizedSpeechType.Hypothesis:
                    case RecognizedSpeechType.Stop:
                        if (lastTs != DateTime.MinValue && DateTime.UtcNow - lastTs > TimeSpan.FromSeconds(1))
                            segment = "";

                        if (e.Words.Length > 0)
                            segment += " " + string.Join(" ", e.Words);

                        segment = segment.Trim();

                        segment = ExecuteCommandPhrase(segment, e.Type == RecognizedSpeechType.Stop);
                        break;

                    case RecognizedSpeechType.Correction:
                    {
                        int lastIdx = segment.LastIndexOf(' ');
                        segment = lastIdx == -1 ? "" : segment.Substring(0, lastIdx);

                        if (e.Words.Length > 0)
                            segment += " " + string.Join(" ", e.Words);

                        segment = segment.Trim();
                        break;
                    }
                }

                lastTs = DateTime.UtcNow;
            }
        }

        public string ExecuteCommandPhrase(string phrase, bool flushPartialMatch)
        {
            if (string.IsNullOrEmpty(phrase))
                return phrase;

            var sw = Stopwatch.StartNew();

            phrase = string.Copy(phrase);

            while (phrase.Length > 0)
            {
                bool isPartialMatch = false;
                int i = 0;
                for (; i < commandDatabase.Commands.Count; ++i)
                {
                    var command = commandDatabase.Commands[i];

                    var parameters = ParsePatternParameters(command.Pattern, phrase);
                    var patternPhrase = ReplacePattern(command.Pattern, parameters);

                    if (phrase.IndexOf(patternPhrase, StringComparison.OrdinalIgnoreCase) != 0)
                        continue;
                    else if (patternPhrase.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        isPartialMatch = isPartialMatch || (
                            ((DateTime.UtcNow - lastTs) < TimeSpan.FromSeconds(0.5)) &&
                            (parameters.Count == 0 || parameters.All(x => !string.IsNullOrEmpty(x.Value)))
                        );
                    }

                    if (!CanCommandExecute(command))
                        continue;

                    Console.WriteLine("Execute: {0}", patternPhrase);
                    NormalizeParameters(command.Pattern, parameters);
                    ExecuteCommand(command, parameters);
                    
                    int len = Math.Min(phrase.Length, patternPhrase.Length);
                    if (len < phrase.Length && phrase[len] == ' ') ++len;
                    phrase = phrase.Remove(0, len);
                    break;
                }

                if (i != commandDatabase.Commands.Count)
                    continue;

                //if (flushPartialMatch && isPartialMatch)
                //if (i == config.CommandDatabase.Commands.Count && isPartialMatch)
                {
                    Console.WriteLine("Unmatched: {0} ({1})", phrase, isPartialMatch);
                    int len = phrase.IndexOf(' ');
                    if (len == -1) len = phrase.Length;
                    phrase = phrase.Remove(0, Math.Min(len + 1, phrase.Length));
                }
                //else
                //    break;
            }

            sw.Stop();
            Console.WriteLine("Tick -> {0}ms", sw.ElapsedMilliseconds);

            return phrase;
        }

        private static string ReplacePattern(string pattern, Dictionary<string, string> parameters)
        {
            pattern = string.Copy(pattern);

            while (true)
            {
                var paramStartIdx = pattern.IndexOf('{');
                if (paramStartIdx == -1)
                    break;

                var paramEndIdx = pattern.IndexOf('}', paramStartIdx) + 1;
                while (paramEndIdx < pattern.Length && (pattern[paramEndIdx] == '+' || pattern[paramEndIdx] == '?'))
                    ++paramEndIdx;

                int colonIdx = pattern.IndexOf(':', paramStartIdx);
                var paramName = pattern.Substring(paramStartIdx + 1, colonIdx - paramStartIdx - 1);

                string paramValue;
                if (!parameters.TryGetValue(paramName, out paramValue))
                {
                    if (pattern[paramEndIdx - 1] != '?')
                        break;

                    paramValue = "";
                }

                var paramFull = pattern.Substring(paramStartIdx, paramEndIdx - paramStartIdx);
                pattern = pattern.Replace(paramFull, paramValue);
            }

            return pattern.TrimEnd();
        }

        private Dictionary<string, string> ParsePatternParameters(string pattern, string text)
        {
            var terminatingWords = commandDatabase.Commands
                .Where(x => x.TerminateDictation)
                .Select(x => x.Pattern)
                .ToList();

            var parameters = new Dictionary<string, string>();

            int i = pattern.IndexOf('{');
            if (i != -1 && text.IndexOf(pattern.Substring(0, i), StringComparison.OrdinalIgnoreCase) == -1)
                return parameters;

            int prevParamEndIdx = -1;
            for (; i != -1; i = pattern.IndexOf('{', i + 1))
            {
                int endIndex = pattern.IndexOf('}', i);
                if (endIndex == -1)
                    break;

                string type = null;
                string key = pattern.Substring(i + 1, endIndex - i - 1);
                int typeIdx = key.IndexOf(':');
                if (typeIdx > endIndex) typeIdx = -1;
                if (typeIdx != -1)
                {
                    type = key.Substring(typeIdx + 1, key.Length - typeIdx - 1);
                    key = key.Substring(0, typeIdx);
                }

                int paramStartIdx, paramEndIdx;

                int startDelimStartIdx = pattern.IndexOf('}', 0, i);
                if (startDelimStartIdx == -1)
                    paramStartIdx = i;
                else
                {
                    string startDelim = pattern.Substring(startDelimStartIdx + 1, i - startDelimStartIdx - 1);
                    paramStartIdx = text.IndexOf(startDelim, StringComparison.OrdinalIgnoreCase);
                    paramStartIdx += startDelim.Length;
                }

                int endDelimStartIdx = pattern.IndexOf('{', endIndex);
                if (endDelimStartIdx == -1)
                    endDelimStartIdx = pattern.IndexOf(' ', endIndex);
                if (endDelimStartIdx == -1)
                {
                    if (endIndex < pattern.Length - 1 && pattern[endIndex + 1] == '+')
                    {
                        int tpIdx = -1;
                        foreach (var termPattern in terminatingWords)
                        {
                            int tppIdx = termPattern.IndexOf('{');
                            var tpSub = " " + (tppIdx != -1 ? termPattern.Substring(0, tppIdx) : termPattern);

                            tpIdx = text.IndexOf(tpSub, i, StringComparison.OrdinalIgnoreCase);
                            if (tpIdx != -1)
                                break;
                        }

                        paramEndIdx = tpIdx != -1 ? tpIdx : text.Length;
                    }
                    else if (i < text.Length)
                    {
                        paramEndIdx = text.IndexOf(' ', i);
                        if (paramEndIdx == -1)
                            paramEndIdx = text.Length;
                    }
                    else
                        paramEndIdx = text.Length;
                }
                else
                {
                    int len = endDelimStartIdx - endIndex - 1;
                    if (len == 0) len = pattern.Length - endIndex - 1;
                    string endDelim = pattern.Substring(endIndex + 1, len);

                    if (prevParamEndIdx == -1)
                        paramEndIdx = text.IndexOf(endDelim, StringComparison.OrdinalIgnoreCase);
                    else
                    {
                        var safeText = text.Substring(prevParamEndIdx);
                        paramEndIdx = safeText.IndexOf(endDelim, StringComparison.OrdinalIgnoreCase) + prevParamEndIdx;
                    }

                    prevParamEndIdx = paramEndIdx + endDelim.Length;
                }

                if (paramStartIdx == -1 || paramEndIdx == -1 || paramEndIdx < paramStartIdx)
                    parameters[key] = "";
                else
                    parameters[key] = text.Substring(paramStartIdx, paramEndIdx - paramStartIdx);

                if (type != null)
                {
                    Func<string, bool> isTerminatingWord;
                    switch (type)
                    {
                        case "number":
                            isTerminatingWord = part => !Numbers.ContainsKey(part);
                            break;
                        case "word":
                        {
                            //var terminatingWords = config.CommandDatabase.Commands
                            //                         .Where(x => x.TerminateDictation)
                            //                         .Select(x => x.Pattern)
                            //                         .ToList();
                            isTerminatingWord = part => terminatingWords.Any(x =>
                            {
                                int endIdx = part.IndexOf('{');
                                return endIdx == -1
                                    ? part.StartsWith(x)
                                    : part.Substring(0, endIdx).StartsWith(x);
                            });
                            break;
                        }
                        default:
                            throw new ArgumentOutOfRangeException("Unknown type: " + type);
                    }

                    var parts = parameters[key].Split(' ');
                    for (var j = 0; j < parts.Length; j++)
                    {
                        var part = parts[j];
                        if (isTerminatingWord(part))
                        {
                            parameters[key] = string.Join(" ", parts, 0, j);
                            break;
                        }
                    }
                }
            }

            return parameters;
        }

        private static void NormalizeParameters(string commandPattern, Dictionary<string, string> parameters)
        {
            if (commandPattern == null)
                throw new ArgumentNullException("commandPattern");

            if (parameters == null)
                throw new ArgumentNullException("parameters");

            foreach (var key in parameters.Keys.ToList())
            {
                var startIdx = commandPattern.IndexOf(key + ":");
                if (startIdx == -1)
                    continue;

                startIdx = startIdx + key.Length + 1;

                var endIdx = commandPattern.IndexOf('}', startIdx);
                var type = commandPattern.Substring(startIdx, endIdx - startIdx);
                switch (type)
                {
                    case "number":
                    {
                        try
                        {
                            int value = 0;
                            var parts = parameters[key].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            for (var i = 0; i < parts.Length; i++)
                            {
                                var part = parts[i];

                                int segmentValue;
                                if (ParseInt(part, out segmentValue))
                                {
                                    if (i == 0)
                                        value = segmentValue;
                                    else if (i == 1)
                                    {
                                        if (segmentValue == 100 || segmentValue == 1000)
                                            value *= segmentValue;
                                        else if (value < 10 && segmentValue >= 10 && segmentValue < 100)
                                        {
                                            value *= 100;
                                            value += segmentValue;
                                        }
                                        else if (value < 10 && segmentValue < 10)
                                            break;
                                        else
                                            value += segmentValue;
                                    }
                                    else
                                    {
                                        if (segmentValue > value)
                                            break;

                                        value += segmentValue;
                                    }
                                }
                            }

                            parameters[key] = value.ToString("G");
                        }
                        catch { /* IGNORE */ }
                        break;
                    }
                }
            }
        }

        public readonly static Dictionary<string, int> Numbers = new Dictionary<string, int>
        {
            { "zero", 0 },
            { "one", 1 },
            { "two", 2 },
            { "three", 3 },
            { "four", 4 },
            { "five", 5 },
            { "six", 6 },
            { "seven", 7 },
            { "eight", 8 },
            { "nine", 9 },
            { "ten", 10 },
            { "eleven", 11 },
            { "twelve", 12 },
            { "thirteen", 13 },
            { "fourteen", 14 },
            { "fifteen", 15 },
            { "sixteen", 16 },
            { "seventeen", 17 },
            { "eighteen", 18 },
            { "nineteen", 19 },
            { "twenty", 20 },
            { "thirty", 30 },
            { "fourty", 40 },
            { "fifty", 50 },
            { "sixty", 60 },
            { "seventy", 70 },
            { "eighty", 80 },
            { "ninety", 90 },
            { "hundred", 100 },
            { "oh", 100 },
            { "thousand", 1000 },
            { "million", 1000000 }
        };

        private static bool ParseInt(string value, out int result)
        {
            return int.TryParse(value, out result) || Numbers.TryGetValue(value, out result);
        }

        private bool CanCommandExecute(CommandConfig command)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (!Enabled && command.Actions[0].Type != "EnableCommandEngine" && command.Actions[0].Type != "Reload")
                return false;

            if (command.Context != null)
            {
                switch (command.Context.Type)
                {
                    case "Context":
                        return String.Equals(Context, command.Context["Value"], StringComparison.OrdinalIgnoreCase);
                    case "Process":
                    {
                        var process = WinApi.GetForegroundProcess();
                        var processName = process != null ? process.ProcessName : "";
                        return string.Equals(processName, command.Context["Value"], StringComparison.OrdinalIgnoreCase);
                    }
                    default:
                        throw new IndexOutOfRangeException(command.Context.Type);
                }
            }

            return true;
        }

        private void ExecuteCommand(CommandConfig command, Dictionary<string, string> parameters)
        {
            if (command == null)
                throw new ArgumentNullException("command");

            if (parameters == null)
                throw new ArgumentNullException("parameters");

            foreach (var action in command.Actions)
            {
                IAction iAction;
                if (!actions.TryGetValue(action.Type, out iAction))
                {
                    Console.WriteLine("Error: Unknown action type {0}", action.Type);
                    continue;
                }

                iAction.Execute(this, action, parameters);
            }
        }
    }
}
