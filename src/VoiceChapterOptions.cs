internal sealed record VoiceChapterOptions(
	string FolderPath,
	string? FfmpegPathOrFolder,
	string TtsProvider,
	string ModelKey,
    bool Transliterate,
	int Rate
);