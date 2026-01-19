using PiperSharp;
﻿using PiperSharp.Models;

internal sealed class PiperTtsService(VoiceChapterOptions options) : ITtsService
{
    private PiperProvider? _provider;
    private string ModelKey => string.IsNullOrWhiteSpace(options.ModelKey) ? "en_GB-alan-medium" : options.ModelKey;
    private async Task InitializeAsync()
    {
        if (_provider != null)
        {
            return;
        }

        var modelKey = ModelKey;
        if (!File.Exists(PiperDownloader.DefaultPiperExecutableLocation))
        {
            await PiperDownloader.DownloadPiper().ExtractPiper(PiperDownloader.DefaultLocation);
        }
        var modelPath = Path.Join(PiperDownloader.DefaultModelLocation, modelKey);
        VoiceModel? model = null;
        if (Directory.Exists(modelPath))
        {
            model = await VoiceModel.LoadModelByKey(modelKey);
        }
        else
        {
            model = await PiperDownloader.DownloadModelByKey(modelKey);
        }

        _provider = new PiperProvider(new PiperConfiguration()
        {
            Model = model,
            UseCuda = false
        });
    }

    public async Task GenerateLabelAsync(string text, string outputWavePath, CancellationToken cancellationToken)
    {
        Console.WriteLine($"  [TTS: Piper] Generating label '{text}' with model '{ModelKey}'");
        await InitializeAsync();
        var data = await _provider!.InferAsync(text, AudioOutputType.Wav, cancellationToken);
        var fs = File.OpenWrite(outputWavePath);
        await fs.WriteAsync(data, cancellationToken);
        await fs.FlushAsync(cancellationToken);
        fs.Close();
    }
}
