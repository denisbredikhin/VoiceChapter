
using System.Speech.Synthesis;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

internal sealed class VoiceChapterWorker(VoiceChapterOptions options, ILogger<VoiceChapterWorker> logger) : BackgroundService
{
    private static readonly string[] SupportedExtensions = [".wav", ".mp3", ".flac", ".m4a", ".ogg", ".aac"];

    private readonly VoiceChapterOptions _options = options;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Processing cancelled.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception during processing.");
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var folderPath = _options.FolderPath;
        var ffmpegArg = _options.FfmpegPathOrFolder;

        logger.LogInformation("Starting VoiceChapter processing for folder: {FolderPath}", folderPath);

        // Configure ffmpeg: either use an explicit path/folder from the user,
        // or let FFMpegCore download and manage ffmpeg automatically.
        if (!string.IsNullOrWhiteSpace(ffmpegArg))
        {
            var providedPath = ffmpegArg!;

            if (File.Exists(providedPath))
            {
                var binFolder = Path.GetDirectoryName(providedPath)!;
                GlobalFFOptions.Configure(new FFOptions
                {
                    BinaryFolder = binFolder,
                    TemporaryFilesFolder = Path.GetTempPath()
                });

                logger.LogInformation("Using ffmpeg binaries from explicit file: {Path}", providedPath);
            }
            else if (Directory.Exists(providedPath))
            {
                GlobalFFOptions.Configure(new FFOptions
                {
                    BinaryFolder = providedPath,
                    TemporaryFilesFolder = Path.GetTempPath()
                });

                logger.LogInformation("Using ffmpeg binaries from folder: {Folder}", providedPath);
            }
            else
            {
                logger.LogError("ffmpeg path or folder not found: {Path}", providedPath);
                return;
            }
        }
        else
        {
            logger.LogInformation("No ffmpeg path provided. Downloading ffmpeg binaries via FFMpegCore...");

            var autoBinaryFolder = Path.Combine(AppContext.BaseDirectory, "ffmpeg-binaries");
            Directory.CreateDirectory(autoBinaryFolder);

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = autoBinaryFolder,
                TemporaryFilesFolder = Path.GetTempPath()
            });

            await FFMpegDownloader.DownloadBinaries(options: GlobalFFOptions.Current);

            logger.LogInformation("ffmpeg binaries downloaded to: {Folder}", autoBinaryFolder);
        }

        if (!Directory.Exists(folderPath))
        {
            logger.LogError("Folder does not exist: {FolderPath}", folderPath);
            return;
        }

        var audioFiles = Directory
            .EnumerateFiles(folderPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (audioFiles.Count == 0)
        {
            logger.LogWarning("No audio files found in the specified folder: {FolderPath}", folderPath);
            return;
        }

        logger.LogInformation("Found {Count} audio file(s) in {FolderPath}.", audioFiles.Count, folderPath);

        // Probe the first file to approximate codec/bitrate settings for all outputs
        long? sourceBitRate = null;
        string? sourceCodecName = null;

        try
        {
            var analysis = await FFProbe.AnalyseAsync(audioFiles[0]);
            var audioStream = analysis.PrimaryAudioStream;

            if (audioStream is not null)
            {
                if (audioStream.BitRate > 0)
                {
                    sourceBitRate = audioStream.BitRate;
                }

                if (!string.IsNullOrWhiteSpace(audioStream.CodecName))
                {
                    sourceCodecName = audioStream.CodecName;
                }

                logger.LogInformation(
                    "Detected source audio settings (from first file) - Codec: {Codec}, Bitrate: {Bitrate} bps",
                    sourceCodecName ?? "unknown",
                    sourceBitRate?.ToString() ?? "unknown");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not analyze source audio with FFProbe.");
        }

        using var synthesizer = new SpeechSynthesizer();

        foreach (var file in audioFiles)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            var baseName = Path.GetFileNameWithoutExtension(file);
            var dir = Path.GetDirectoryName(file)!;

            var labelWavPath = Path.Combine(dir, baseName + "_label_temp.wav");
            var outputPath = Path.Combine(dir, baseName + "_labeled" + Path.GetExtension(file));

            logger.LogInformation("Processing: {FileName}", fileName);

            try
            {
                logger.LogInformation("  Generating spoken label...");
                synthesizer.SetOutputToWaveFile(labelWavPath);
                synthesizer.Speak(fileName);
                synthesizer.SetOutputToDefaultAudioDevice();

                logger.LogInformation("  Concatenating label with original using ffmpeg (via FFMpegCore)...");

                await FFMpegArguments
                    .FromFileInput(labelWavPath)
                    .AddFileInput(file)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        var customArgs = "-filter_complex \"[0:a][1:a]concat=n=2:v=0:a=1[a]\" -map \"[a]\"";

                        if (!string.IsNullOrWhiteSpace(sourceCodecName))
                        {
                            customArgs += $" -c:a {sourceCodecName}";
                        }

                        if (sourceBitRate.HasValue)
                        {
                            customArgs += $" -b:a {sourceBitRate.Value}";
                        }

                        options.WithCustomArgument(customArgs);
                    })
                    .ProcessAsynchronously(true, new FFOptions
                    {
                        BinaryFolder = GlobalFFOptions.Current.BinaryFolder,
                        TemporaryFilesFolder = GlobalFFOptions.Current.TemporaryFilesFolder,
                        WorkingDirectory = GlobalFFOptions.Current.WorkingDirectory
                    });

                logger.LogInformation("  Done -> {OutputFile}", Path.GetFileName(outputPath));
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "  Error while processing {FileName}.", fileName);
            }
            finally
            {
                try
                {
                    if (File.Exists(labelWavPath))
                    {
                        File.Delete(labelWavPath);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(ex, "Failed to delete temporary label file {TempFile}.", labelWavPath);
                }
            }
        }

        logger.LogInformation("Processing finished.");
    }
}