using System.Collections.Generic;

namespace Parrot
{
    public interface ISpeechRecognitionService
    {
        void Initialize();

        void Shutdown();

        void OnSpeechRecognition(RecognizedSpeechEventArgs args);
    }
    
    public sealed class RecognizedSpeechEventArgs
    {
        public string[] Words { get; set; }

        public RecognizedSpeechType Type { get; set; }
    }

    public enum RecognizedSpeechType
    {
        Hypothesis,
        Correction,
        Stop
    }
}