using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args.Length == 0)
{
    Console.WriteLine("Usage: VoiceChapter <folderPath> [ffmpegPathOrFolder] [ttsProvider] [modelKey]");
    Console.WriteLine("  <folderPath>          Folder containing audio files to process.");
    Console.WriteLine("  [ffmpegPathOrFolder] Optional path to ffmpeg.exe or its folder.");
    Console.WriteLine("                        If omitted, FFMpegCore will download ffmpeg automatically.");
    Console.WriteLine("  [ttsProvider]         Optional TTS engine: 'speech' (default) or 'piper'.");
    Console.WriteLine("  [modelKey]            Optional Piper model key (default: en_GB-alan-medium).");
    return;
}

var options = new VoiceChapterOptions(
    FolderPath: args[0],
    FfmpegPathOrFolder: args.Length > 1 ? args[1] : null,
    TtsProvider: ParseTtsProvider(args.Length > 2 ? args[2] : null),
    ModelKey: ParseModelKey(args.Length > 3 ? args[3] : null),
    Transliterate: args.Any(a => a.StartsWith("--translit", StringComparison.OrdinalIgnoreCase))
);

using var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "HH:mm:ss ";
        });

        logging.SetMinimumLevel(LogLevel.Information);
    })
    .ConfigureServices(services =>
    {
        services.AddSingleton(options);
        if (options.TtsProvider == TtsProvider.Piper)
        {
            services.AddSingleton<ITtsService, PiperTtsService>();
        }
        else
        {
            services.AddSingleton<ITtsService, SpeechSynthesizerTtsService>();
        }
        services.AddHostedService<VoiceChapterWorker>();
    })
    .Build();

await host.RunAsync();

static TtsProvider ParseTtsProvider(string? value)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return TtsProvider.Speech;
    }

    return value.Trim().ToLowerInvariant() switch
    {
        "piper" => TtsProvider.Piper,
        "speech" => TtsProvider.Speech,
        _ => TtsProvider.Speech
    };
}

static string ParseModelKey(string? value)
{
    const string DefaultModelKey = "en_GB-alan-medium";
    return string.IsNullOrWhiteSpace(value) ? DefaultModelKey : value.Trim();
}
