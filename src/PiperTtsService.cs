using PiperSharp;
﻿using PiperSharp.Models;

internal sealed class PiperTtsService : ITtsService
{
    private PiperProvider? _provider;
    private async Task InitializeAsync(VoiceChapterOptions options)
    {
        if (_provider != null)
        {
            return;
        }
        Console.WriteLine($"  [TTS: Piper] Initializing...");
        var modelKey = string.IsNullOrWhiteSpace(options.ModelKey) ? "en_GB-alan-medium" : options.ModelKey;
        if (!File.Exists(PiperDownloader.DefaultPiperExecutableLocation))
        {
            Console.WriteLine($"  [TTS: Piper] Downloading Piper...");
            await PiperDownloader.DownloadPiper().ExtractPiper(PiperDownloader.DefaultLocation);
        }
        var modelPath = Path.Join(PiperDownloader.DefaultModelLocation, modelKey);
        if (Directory.Exists(modelPath))
        {
            Console.WriteLine($"  [TTS: Piper] Found existing model at '{modelPath}'");
        }
        else
        {
            Console.WriteLine($"  [TTS: Piper] Downloading model '{modelKey}'...");
        }

        var model = 
            Directory.Exists(modelPath) ?
            await VoiceModel.LoadModelByKey(modelKey) : 
            await PiperDownloader.DownloadModelByKey(modelKey);

        _provider = new PiperProvider(new PiperConfiguration()
        {
            Model = model,
            UseCuda = false, 
            SpeakingRate = options.Rate switch
            {
                0 => 1f,
                // -1 -> 1.33, -2 -> 1.66, -3 -> 2, -4 -> 2.33, ...
                < 0 => 1 - options.Rate / 3,
                // 1 -> 0.75, 2 -> 0.5, 3 -> 0,375
                >0 => 1.5f / (options.Rate+1)
            }
        });
    }

    public async Task GenerateLabelAsync(
        VoiceChapterOptions options, 
        string text, string outputWavePath, CancellationToken cancellationToken)
    {
        await InitializeAsync(options);
        Console.WriteLine($"  [TTS: Piper] Generating label '{text}' with model '{options.ModelKey}'");
        var data = await _provider!.InferAsync(text, AudioOutputType.Wav, cancellationToken);
        var fs = File.OpenWrite(outputWavePath);
        await fs.WriteAsync(data, cancellationToken);
        await fs.FlushAsync(cancellationToken);
        fs.Close();
    }
}
