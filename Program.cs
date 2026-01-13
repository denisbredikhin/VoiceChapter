using System.Speech.Synthesis;
using System.Runtime.Versioning;
using FFMpegCore;
using FFMpegCore.Extensions.Downloader;

[assembly: SupportedOSPlatform("windows")]

if (args.Length == 0)
{
    Console.WriteLine("Usage: VoiceChapter <folderPath> [ffmpegPathOrFolder]");
    Console.WriteLine("  <folderPath>          Folder containing audio files to process.");
    Console.WriteLine("  [ffmpegPathOrFolder] Optional path to ffmpeg.exe or its folder.");
    Console.WriteLine("                        If omitted, FFMpegCore will download ffmpeg automatically.");
    return;
}

var folderPath = args[0];
var ffmpegArg = args.Length > 1 ? args[1] : null;

// Configure ffmpeg: either use an explicit path/folder from the user,
// or let FFMpegCore download and manage ffmpeg automatically.
if (!string.IsNullOrWhiteSpace(ffmpegArg))
{
    var providedPath = ffmpegArg!;

    if (File.Exists(providedPath))
    {
        // User passed a direct path to ffmpeg.exe
        var binFolder = Path.GetDirectoryName(providedPath)!;
        GlobalFFOptions.Configure(new FFOptions
        {
            BinaryFolder = binFolder,
            TemporaryFilesFolder = Path.GetTempPath()
        });
    }
    else if (Directory.Exists(providedPath))
    {
        // User passed a folder that should contain ffmpeg/ffprobe
        GlobalFFOptions.Configure(new FFOptions
        {
            BinaryFolder = providedPath,
            TemporaryFilesFolder = Path.GetTempPath()
        });
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
}

if (!Directory.Exists(folderPath))
{
    Console.WriteLine($"Folder does not exist: {folderPath}");
    return;
}

string[] extensions = [".wav", ".mp3", ".flac", ".m4a", ".ogg", ".aac"];
var audioFiles = Directory
    .EnumerateFiles(folderPath)
    .Where(f => extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
    .OrderBy(f => f)
    .ToList();

if (audioFiles.Count == 0)
{
    Console.WriteLine("No audio files found in the specified folder.");
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

        Console.WriteLine("Detected source audio settings (from first file):");
        Console.WriteLine($"  Codec:   {sourceCodecName ?? "unknown"}");
        Console.WriteLine($"  Bitrate: {(sourceBitRate.HasValue ? sourceBitRate + " bps" : "unknown")}");
        Console.WriteLine("  Sample:  unknown");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Warning: could not analyze source audio with FFProbe: {ex.Message}");
}

using var synthesizer = new SpeechSynthesizer();

foreach (var file in audioFiles)
{
    var fileName = Path.GetFileName(file);
    var baseName = Path.GetFileNameWithoutExtension(file);
    var dir = Path.GetDirectoryName(file)!;

    var labelWavPath = Path.Combine(dir, baseName + "_label_temp.wav");
    var outputPath = Path.Combine(dir, baseName + "_labeled" + Path.GetExtension(file));

    Console.WriteLine();
    Console.WriteLine($"Processing: {fileName}");

    try
    {
        // 1) Generate spoken filename as WAV using System.Speech
        Console.WriteLine("  Generating spoken label...");
        synthesizer.SetOutputToWaveFile(labelWavPath);
        synthesizer.Speak(fileName);
        synthesizer.SetOutputToDefaultAudioDevice();

        // 2) Use FFMpegCore to concat label audio + original audio
        Console.WriteLine("  Concatenating label with original using ffmpeg (via FFMpegCore)...");

        // Equivalent to:
        // ffmpeg -y -i label.wav -i original.ext -filter_complex "[0:a][1:a]concat=n=2:v=0:a=1[a]" -map "[a]" output.ext
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
                    // ffmpeg accepts bitrate in bits per second as an integer
                    customArgs += $" -b:a {sourceBitRate.Value}";
                }

                options.WithCustomArgument(customArgs);
            })
            .ProcessAsynchronously();

        Console.WriteLine($"  Done -> {Path.GetFileName(outputPath)}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Error: {ex.Message}");
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
        catch
        {
            // Ignore cleanup errors
        }
    }
}

Console.WriteLine();
Console.WriteLine("Processing finished.");
