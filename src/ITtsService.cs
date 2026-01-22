internal interface ITtsService
{
    Task GenerateLabelAsync(
        VoiceChapterOptions options,
        string text, 
        string outputWavePath, 
        CancellationToken cancellationToken);
}
