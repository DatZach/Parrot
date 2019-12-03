using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Speech.Recognition;
using System.Speech.Recognition.SrgsGrammar;
using System.Threading;
using Newtonsoft.Json;

namespace Parrot
{
    // TODO Still kind of a god object, split service discovery/dispatching from speech recognition/dispatching
    public sealed class SpeechServiceManager
    {
        public bool Running { get; private set; }

        private readonly Dictionary<string, ISpeechRecognitionService> services;
        private readonly SpeechRecognitionEngine engine;
        private readonly Dictionary<string, string> corrections;

        private RecognitionResult prevRecognitionResult;

        public SpeechServiceManager()
        {
            services = new Dictionary<string, ISpeechRecognitionService>();
            corrections = new Dictionary<string, string>();

            var cultureInfo = new CultureInfo(Program.Config.CultureCode);
            engine = new SpeechRecognitionEngine(cultureInfo)
            {
                InitialSilenceTimeout = TimeSpan.Zero,
                EndSilenceTimeout = TimeSpan.Zero,
                EndSilenceTimeoutAmbiguous = TimeSpan.Zero
            };

            engine.SpeechRecognized += Engine_SpeechRecognized;
            engine.SpeechRecognitionRejected += Engine_SpeechRecognitionRejected;
            engine.SpeechHypothesized += Engine_SpeechHypothesized;

            var asm = Assembly.GetExecutingAssembly();
            foreach (var type in asm.GetTypes()
                .Where(x => typeof(ISpeechRecognitionService).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract))
            {
                services[type.Name] = (ISpeechRecognitionService)Activator.CreateInstance(type);
            }
        }

        public void Initialize()
        {
            // Initialize Engine
            engine.SetInputToDefaultAudioDevice();
            engine.UnloadAllGrammars();

            // Load Grammar
            var commandDatabase = CommandDatabaseConfig.FromDirectory(Program.Config.CommandDatabaseDirectory);
            var grammars = GrammarGenerator.FromConfig(commandDatabase);
            foreach (var grammar in grammars)
                engine.LoadGrammar(grammar);

            // Load Corrections
            var correctionsContent = File.ReadAllText(Program.Config.CorrectionsDictionaryPath);
            var correctionsUnloaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(correctionsContent);

            corrections.Clear();
            foreach (var kvp in correctionsUnloaded)
                corrections.Add(kvp.Key, kvp.Value);

            // Initialize Services
            foreach (var service in services.Values)
                service.Initialize();
        }

        public void Shutdown()
        {
            foreach (var service in services.Values)
                service.Shutdown();

            engine.RecognizeAsyncStop();
        }

        public void Run()
        {
            if (Running)
                return;

            engine.RecognizeAsync(RecognizeMode.Multiple);
            Running = true;

            while(Running)
                Thread.Sleep(100);
        }

        private void Engine_SpeechHypothesized(object sender, SpeechHypothesizedEventArgs e)
        {
            return;
            if (e.Result.Confidence < 0.85)
                return;

            Console.WriteLine("! Hypothesis {0} - {1}", e.Result.Confidence, e.Result.Text);

            DispatchResult(e.Result, RecognizedSpeechType.Hypothesis);
        }
        
        private void Engine_SpeechRecognized(object sender, SpeechRecognizedEventArgs e)
        {
            DispatchResult(e.Result, RecognizedSpeechType.Stop);
            prevRecognitionResult = null;
        }

        private void Engine_SpeechRecognitionRejected(object sender, SpeechRecognitionRejectedEventArgs e)
        {
            prevRecognitionResult = null;
        }

        private void DispatchResult(RecognitionResult result, RecognizedSpeechType type)
        {
            var curRecognitionResult = result;

            if (prevRecognitionResult != null &&
                Math.Abs(curRecognitionResult.Confidence - prevRecognitionResult.Confidence) > 0.25)
            {
                Console.WriteLine("! Abandoned");
                return;
            }
            else
                Console.Write("! DispatchResult ");
            

            var prevText = prevRecognitionResult == null ? "" : prevRecognitionResult.Text;
            var curText = curRecognitionResult.Text;
            int i = 0;
            for (; i < prevText.Length && i < curText.Length; ++i)
                if (prevText[i] != curText[i])
                    break;
            bool isCorrection = prevText != "" && i < curText.Length && curText[i] != ' ';
            for(; i > 0 && i < curText.Length; --i)
                if (curText[i] == ' ')
                    break;

            curText = curText.Substring(i, curText.Length - i);
            curText = curText.Replace(",", " comma ");
            var words = curText.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            for (int j = 0; j < words.Length; ++j)
            {
                words[j] = new string(words[j].Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

                foreach (var correction in corrections)
                    words[j] = words[j].Replace(correction.Key, correction.Value);
            }

            Console.WriteLine("{0} - {1}", result.Confidence, curText);

            prevRecognitionResult = curRecognitionResult;

            var args = new RecognizedSpeechEventArgs
            {
                Type = isCorrection ? RecognizedSpeechType.Correction : type,
                Words = words
            };

            foreach (var service in services.Values)
                service.OnSpeechRecognition(args);
        }
    }

    internal sealed class GrammarGenerator
    {
        private static Grammar GenerateCommandGrammar(List<CommandConfig> commands)
        {
            var doc = new SrgsDocument();

            var numbers = new List<string>(CommandEngine.Numbers.Keys);
            numbers.AddRange(new [] { "left", "up", "right", "down" });

            var numbers04 = new SrgsRule("numbers04") { Scope = SrgsRuleScope.Private };
            numbers04.Add(new SrgsItem(0, 4, new SrgsOneOf(numbers.ToArray())));
            doc.Rules.Add(numbers04);

            var numbers14 = new SrgsRule("numbers14") { Scope = SrgsRuleScope.Private };
            numbers14.Add(new SrgsItem(1, 4, new SrgsOneOf(numbers.ToArray())));
            doc.Rules.Add(numbers14);

            // TODO Prevent duplicate commands from being registered
            var choices = new List<SrgsItem>();
            foreach (var command in commands)
            {
                var item = new SrgsItem();
                
                var parts = command.Pattern.Split(' ');
                for (int i = 0, j = 0; i <= parts.Length; ++i)
                {
                    if (i == parts.Length)
                    {
                        var segment = string.Join(" ", parts, j, i - j);
                        if (!string.IsNullOrWhiteSpace(segment))
                        {
                            item.Add(new SrgsText(segment));
                            //Console.Write("\"" + segment + "\" ");
                        }
                    }
                    else if (parts[i].IndexOf('{') != -1)
                    {
                        var segment = string.Join(" ", parts, j, i - j);
                        if (!string.IsNullOrWhiteSpace(segment))
                        {
                            item.Add(new SrgsText(segment));
                            //Console.Write("\"" + segment + "\" ");
                        }

                        var type = parts[i].Split(':').Skip(1).First();
                        bool zeroOrMore = type[type.Length - 1] == '?';
                        type = type.Substring(0, type.IndexOf('}'));
                        switch (type)
                        {
                            case "number":
                                item.Add(new SrgsRuleRef(zeroOrMore ? numbers04 : numbers14));
                                //Console.Write("*number* ");
                                break;
                            case "word":
                                item.Add(SrgsRuleRef.Dictation);
                                //Console.Write("*word* ");
                                break;
                            default:
                                throw new IndexOutOfRangeException(type);
                        }

                        j = i + 1;
                    }
                }

                choices.Add(item);
                //Console.WriteLine();
            }

            var root = new SrgsRule("root") { Scope = SrgsRuleScope.Private };
            root.Add(new SrgsItem(
                1, int.MaxValue,
                new SrgsOneOf(choices.ToArray())
            ));
            doc.Rules.Add(root);
            doc.Root = root;

            //using(var sw = File.OpenWrite(@"C:\Temp\NewGrammar.srgs"))
            //using(var xw = XmlWriter.Create(sw))
            //    doc.WriteSrgs(xw);

            return new Grammar(doc)
            {
                Name = "Commands",
                Weight = 1.0f
            };
        }

        public static IEnumerable<Grammar> FromConfig(CommandDatabaseConfig config)
        {
            if (config == null)
                throw new ArgumentNullException("config");
            
            yield return GenerateCommandGrammar(config.Commands);
        }
    }
}
