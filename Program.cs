using System.Runtime.Versioning;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;

var app = ConsoleApp.Create()
    .ConfigureServices((_, services) =>
    {
        services.AddSingleton<SpeechSynthesizerTtsService>();
        services.AddSingleton<PiperTtsService>();
        services.AddSingleton<ITtsService>(sp =>
        {
            // Default ITtsService; actual engine is selected per-run in the worker based on options.
            return sp.GetRequiredService<SpeechSynthesizerTtsService>();
        });
        services.AddSingleton<VoiceChapterWorker>();
    });

// Root command: processes a folder of audio files with optional ffmpeg path, TTS provider, model key, and transliteration.
app.Add("", async Task (
    [FromServices] VoiceChapterWorker worker,
    string folder,
    string? ffmpeg = null,
    TtsProvider provider = TtsProvider.Speech,
    string modelKey = "en_GB-alan-medium",
    bool transliterate = false,
    CancellationToken cancellationToken = default) =>
{
    var options = new VoiceChapterOptions(
        FolderPath: folder,
        FfmpegPathOrFolder: ffmpeg,
        TtsProvider: provider,
        ModelKey: modelKey,
        Transliterate: transliterate
    );

    await worker.RunAsync(options, cancellationToken);
});

await app.RunAsync(args);
