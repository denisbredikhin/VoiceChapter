using PiperSharp;
﻿using PiperSharp.Models;

internal sealed class PiperTtsService : ITtsService
{
    private PiperProvider? _provider;
    private async Task InitializeAsync()
    {
        if (_provider != null)
        {
            return;
        }
        const string ModelKey = "en_GB-alan-medium";
        if (!File.Exists(PiperDownloader.DefaultPiperExecutableLocation))
        {
            await PiperDownloader.DownloadPiper().ExtractPiper(PiperDownloader.DefaultLocation);
        }

        var modelPath = Path.Join(PiperDownloader.DefaultModelLocation, ModelKey);
        VoiceModel? model = null;
        if (Directory.Exists(modelPath))
        {
            model = await VoiceModel.LoadModelByKey(ModelKey);
        }
        else
        {
            model = await PiperDownloader.DownloadModelByKey(ModelKey);
        }

        _provider = new PiperProvider(new PiperConfiguration()
        {
            Model = model,
            UseCuda = false
        });
    }

    public async Task GenerateLabelAsync(string text, string outputWavePath, CancellationToken cancellationToken)
    {
        await InitializeAsync();
        var data = await _provider!.InferAsync(text, AudioOutputType.Wav, cancellationToken);
        var fs = File.OpenWrite(outputWavePath);
        await fs.WriteAsync(data, cancellationToken);
        await fs.FlushAsync(cancellationToken);
        fs.Close();
    }
}
