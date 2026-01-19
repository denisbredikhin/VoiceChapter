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
            // Default ITtsService; actual engine is selected per-run based on options.
            return sp.GetRequiredService<SpeechSynthesizerTtsService>();
        });
        services.AddSingleton<VoiceChapterWorker>();
    });

// Register commands via class-based approach.
app.Add<MyCommands>();

await app.RunAsync(args);
