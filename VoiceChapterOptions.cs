internal enum TtsProvider
{
	Speech,
	Piper
}

internal sealed record VoiceChapterOptions(string FolderPath, string? FfmpegPathOrFolder, TtsProvider TtsProvider);