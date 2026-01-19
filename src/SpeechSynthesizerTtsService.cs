using System.Runtime.Versioning;
using System.Speech.Synthesis;


[SupportedOSPlatform("windows")]
internal sealed class SpeechSynthesizerTtsService : ITtsService, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();

    public Task GenerateLabelAsync(string text, string outputWavePath, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [TTS: SpeechSynthesizer] Generating label '{text}'");
        cancellationToken.ThrowIfCancellationRequested();
        _synthesizer.SetOutputToWaveFile(outputWavePath);
        _synthesizer.Speak(text);
        _synthesizer.SetOutputToDefaultAudioDevice();

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _synthesizer.Dispose();
    }
}
