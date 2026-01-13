using System.Diagnostics;
using System.Speech.Synthesis;

if (args.Length == 0)
{
    Console.WriteLine("Usage: AudioLabeler <folderPath> [ffmpegPath]");
    Console.WriteLine("  <folderPath>  Folder containing audio files to process.");
    Console.WriteLine("  [ffmpegPath] Optional path to ffmpeg.exe (defaults to 'ffmpeg' in PATH).");
    return;
}

var folderPath = args[0];
var ffmpegPath = args.Length > 1 ? args[1] : "ffmpeg";

// If an explicit path to ffmpeg.exe is provided, make sure it exists
bool ffmpegIsExplicitPath =
    ffmpegPath.Contains(Path.DirectorySeparatorChar) ||
    ffmpegPath.Contains(Path.AltDirectorySeparatorChar) ||
    ffmpegPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase);

if (ffmpegIsExplicitPath && !File.Exists(ffmpegPath))
{
    Console.WriteLine($"ffmpeg not found at '{ffmpegPath}'.");
    Console.WriteLine("Make sure the path points to ffmpeg.exe (often in a 'bin' subfolder).");
    Console.WriteLine(@"Example: C:\\Soft\\ffmpeg\\bin\\ffmpeg.exe");
    return;
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

        // 2) Use ffmpeg to concat label audio + original audio
        Console.WriteLine("  Concatenating label with original using ffmpeg...");

        // ffmpeg command:
        // ffmpeg -y -i label.wav -i original.ext -filter_complex "[0:a][1:a]concat=n=2:v=0:a=1[a]" -map "[a]" output.ext
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = $"-y -i \"{labelWavPath}\" -i \"{file}\" -filter_complex \"[0:a][1:a]concat=n=2:v=0:a=1[a]\" -map \"[a]\" \"{outputPath}\"",
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = false,
            CreateNoWindow = true
        };

        var process = Process.Start(psi);                                                                                                                                                    
        if (process == null)
        {
            Console.WriteLine("  Failed to start ffmpeg process.");
        }
        else
        {
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"  ffmpeg failed with exit code {process.ExitCode}.");
            }
            else
            {
                Console.WriteLine($"  Done -> {Path.GetFileName(outputPath)}");
            }
        }
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
