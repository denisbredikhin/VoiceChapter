using FFMpegCore;
using FFMpegCore.Extensions.Downloader;
using Microsoft.Extensions.Hosting;
using NickBuhro.Translit;

internal sealed class VoiceChapterWorker(VoiceChapterOptions options, ITtsService ttsService, IHostApplicationLifetime appLifetime) : BackgroundService
{
    private static readonly string[] SupportedExtensions = [".wav", ".mp3", ".flac", ".m4a", ".ogg", ".aac"];

    private readonly VoiceChapterOptions _options = options;
    private readonly ITtsService _ttsService = ttsService;
    private readonly IHostApplicationLifetime _appLifetime = appLifetime;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RunAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("Processing cancelled.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unhandled exception during processing: {ex}");
        }
        finally
        {
            _appLifetime.StopApplication();
        }
    }

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        var folderPath = _options.FolderPath;
        var ffmpegArg = _options.FfmpegPathOrFolder;

        Console.WriteLine($"Starting VoiceChapter processing for folder: {folderPath}");

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

                Console.WriteLine($"Using ffmpeg binaries from explicit file: {providedPath}");
            }
            else if (Directory.Exists(providedPath))
            {
                GlobalFFOptions.Configure(new FFOptions
                {
                    BinaryFolder = providedPath,
                    TemporaryFilesFolder = Path.GetTempPath()
                });

                Console.WriteLine($"Using ffmpeg binaries from folder: {providedPath}");
            }
            else
            {
                Console.WriteLine($"ffmpeg path or folder not found: {providedPath}");
                return;
            }
        }
        else
        {
            Console.WriteLine("No ffmpeg path provided. Downloading ffmpeg binaries via FFMpegCore...");

            var autoBinaryFolder = Path.Combine(AppContext.BaseDirectory, "ffmpeg-binaries");
            Directory.CreateDirectory(autoBinaryFolder);

            GlobalFFOptions.Configure(new FFOptions
            {
                BinaryFolder = autoBinaryFolder,
                TemporaryFilesFolder = Path.GetTempPath()
            });

            await FFMpegDownloader.DownloadBinaries(options: GlobalFFOptions.Current);

            Console.WriteLine($"ffmpeg binaries downloaded to: {autoBinaryFolder}");
        }

        if (!Directory.Exists(folderPath))
        {
            Console.WriteLine($"Folder does not exist: {folderPath}");
            return;
        }

        var audioFiles = Directory
            .EnumerateFiles(folderPath)
            .Where(f => SupportedExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f)
            .ToList();

        if (audioFiles.Count == 0)
        {
            Console.WriteLine($"No audio files found in the specified folder: {folderPath}");
            return;
        }

        Console.WriteLine($"Found {audioFiles.Count} audio file(s) in {folderPath}.");

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

                Console.WriteLine(
                    $"Detected source audio settings (from first file) - Codec: {sourceCodecName ?? "unknown"}, Bitrate: {sourceBitRate?.ToString() ?? "unknown"} bps");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Could not analyze source audio with FFProbe: {ex}");
        }

        foreach (var file in audioFiles)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var fileName = Path.GetFileName(file);
            var baseName = Path.GetFileNameWithoutExtension(file);
            var dir = Path.GetDirectoryName(file)!;

            var labelWavPath = Path.Combine(dir, baseName + "_label_temp.wav");
            var outputPath = Path.Combine(dir, baseName + "_labeled" + Path.GetExtension(file));

            Console.WriteLine($"Processing: {fileName}");

            try
            {
                var label = baseName;
                if (_options.Transliterate)
                    label = Transliteration.LatinToCyrillyc(label);
                await _ttsService.GenerateLabelAsync(label, labelWavPath, stoppingToken);

                Console.WriteLine("  Concatenating label with original using ffmpeg (via FFMpegCore)...");

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

                Console.WriteLine($"  Done -> {Path.GetFileName(outputPath)}");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine($"  Error while processing {fileName}: {ex}");
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
                    Console.WriteLine($"Failed to delete temporary label file {labelWavPath}: {ex}");
                }
            }
        }

        Console.WriteLine("Processing finished.");
    }
}