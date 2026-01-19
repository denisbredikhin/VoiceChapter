using System.Threading;
using System.Threading.Tasks;
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
    /// <param name="transliterate">-tr,Whether to transliterate file names before TTS.</param>
    [Command("")]
    public async Task Root(
        string folder,
        string? ffmpeg = null,
        TtsProvider provider = TtsProvider.Speech,
        string modelKey = "en_GB-alan-medium",
        bool transliterate = false,
        CancellationToken cancellationToken = default)
    {
        var options = new VoiceChapterOptions(
            FolderPath: folder,
            FfmpegPathOrFolder: ffmpeg,
            TtsProvider: provider,
            ModelKey: modelKey,
            Transliterate: transliterate
        );

        await worker.RunAsync(options, cancellationToken);
    }
}
