using System.Runtime.Versioning;
using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;

var app = ConsoleApp.Create()
    .ConfigureServices((_, services) =>
    {
        services.AddKeyedSingleton<ITtsService, SpeechSynthesizerTtsService>("speech");
        services.AddKeyedSingleton<ITtsService, PiperTtsService>("piper");
        services.AddSingleton<VoiceChapterWorker>();
    });

// Register commands via class-based approach.
app.Add<MyCommands>();

await app.RunAsync(args);
