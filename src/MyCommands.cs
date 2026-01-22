using ConsoleAppFramework;

internal sealed class MyCommands(VoiceChapterWorker worker)
{

    /// <summary>
    /// Process audio files in a folder, prepending spoken labels.
    /// </summary>
    /// <param name="folder">-f,Folder containing audio files to process.</param>
    /// <param name="ffmpeg">-fe,Optional path to ffmpeg executable or its folder.</param>
    /// <param name="provider">-p,TTS provider to use (speech or piper).</param>
    /// <param name="modelKey">-m,Piper model key (when provider is piper).</param>
    /// <param name="translit">-tr,Whether to transliterate file names before TTS.</param>
    /// <param name="rate">-r,Speaking rate, from -10 to 10</param>
    [Command("")]
    public async Task Root(
        string folder,
        string? ffmpeg = null,
        string provider = "speech",
        string modelKey = "en_GB-alan-medium",
        bool translit = false,
        int rate = 0,
        CancellationToken cancellationToken = default)
    {
        var options = new VoiceChapterOptions(
            FolderPath: folder,
            FfmpegPathOrFolder: ffmpeg,
            TtsProvider: provider.ToLowerInvariant(),
            ModelKey: modelKey,
            Transliterate: translit,
            Rate: rate
        );

        await worker.RunAsync(options, cancellationToken);
    }
}
