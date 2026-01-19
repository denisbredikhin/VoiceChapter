internal interface ITtsService
{
    Task GenerateLabelAsync(string text, string outputWavePath, CancellationToken cancellationToken);
}
