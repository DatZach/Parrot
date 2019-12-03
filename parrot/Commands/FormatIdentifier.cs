using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Parrot.Commands
{
    [Action]
    public sealed class FormatIdentifier : IAction
    {
        public void Execute(CommandEngine engine, ActionConfig action, Dictionary<string, string> args)
        {
            try
            {
                var parts = args["value"].Split(' ');
                var style = action["Style"];
                string sep = "";

                for (int i = 0; i < parts.Length; ++i)
                {
                    string sub;
                    if (substitutes.TryGetValue(parts[i], out sub))
                        parts[i] = sub;
                }

                switch (style)
                {
                    case "Camel":
                        for (int i = 0; i < parts.Length; ++i)
                            parts[i] = i > 0 ? FirstLetterToUpper(parts[i]) : FirstLetterToLower(parts[i]);
                        break;
                    case "Pascal":
                        for (int i = 0; i < parts.Length; ++i)
                            parts[i] = FirstLetterToUpper(parts[i]);
                        break;
                    case "Stick":
                        sep = "-";
                        for (int i = 0; i < parts.Length; ++i)
                            parts[i] = parts[i].ToLowerInvariant();
                        break;
                    case "Snake":
                        sep = "_";
                        for (int i = 0; i < parts.Length; ++i)
                            parts[i] = parts[i].ToLowerInvariant();
                        break;
                    case "ShoutingSnake":
                        sep = "_";
                        for (int i = 0; i < parts.Length; ++i)
                            parts[i] = parts[i].ToUpperInvariant();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
                string ident = string.Join(sep, parts);
                SendKeys.SendWait(ident);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: {0}", ex.Message);
            }
        }

        private string FirstLetterToUpper(string str)
        {
            if (str == null)
                return null;

            if (str.Length > 1)
                return char.ToUpper(str[0]) + str.Substring(1);

            return str.ToUpper();
        }

        private string FirstLetterToLower(string str)
        {
            if (str == null)
                return null;
            
            if (str.Length > 1)
                return char.ToLower(str[0]) + str.Substring(1);

            return str.ToUpper();
        }

        public readonly static Dictionary<string, string> substitutes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "alpha", "a" },
            { "bravo", "b" },
            { "charlie", "c" },
            { "delta", "d" },
            { "echo", "e" },
            { "foxtrot", "f" },
            { "golf", "g" },
            { "hotel", "h" },
            { "india", "i" },
            { "juliet", "j" },
            { "kilo", "k" },
            { "lima", "l" },
            { "mike", "m" },
            { "november", "n" },
            { "oscar", "o" },
            { "papa", "p" },
            { "quebec", "q" },
            { "romeo", "r" },
            { "sierra", "s" },
            { "tango", "t" },
            { "uniform", "u" },
            { "victor", "v" },
            { "wiskey", "w" },
            { "x-ray", "x" },
            { "yankee", "y" },
            { "zulu", "z" }
        };
    }
}
