# Copilot Instructions for VoiceChapter

## Project Overview
- **VoiceChapter** is a .NET 10.0 console application for batch-processing audio files in a folder.
- It generates a spoken label (using the filename) for each audio file, prepends it to the audio, and outputs a new labeled file.
- Uses `System.Speech.Synthesis` for TTS and invokes `ffmpeg` for audio concatenation.

## Key Files
- `Program.cs`: Main entry point and all logic for file discovery, TTS, and ffmpeg invocation.
- `VoiceChapter.csproj`: Project configuration, targets .NET 10.0, references `System.Speech`.
- No additional project files, submodules, or custom build scripts.

## Build & Run
- Build with Visual Studio or `dotnet build`.
- Run from command line:
  ```
  dotnet run -- <folderPath> [ffmpegPath]
  ```
  - `<folderPath>`: Directory with audio files (.wav, .mp3, .flac, .m4a, .ogg, .aac)
  - `[ffmpegPath]`: Optional, path to `ffmpeg.exe` (defaults to `ffmpeg` in PATH)
- Output files are named `<original>_labeled.<ext>` in the same folder.

## Developer Workflows
- No tests or test projects are present.
- Debugging: Use Visual Studio or `dotnet run` with breakpoints in `Program.cs`.
- All logic is in a single file; no DI, services, or layers.

## Patterns & Conventions
- All processing is synchronous and linear.
- Temporary label audio is always named `<base>_label_temp.wav` and deleted after use.
- Error handling is via `try/catch` around each file's processing.
- Only standard .NET and `System.Speech` APIs are used; no custom helpers or wrappers.

## External Dependencies
- Requires `ffmpeg` (must be in PATH or provided as argument).
- Only NuGet dependency is `System.Speech` (see `.csproj`).

## Extending/Modifying
- Add new audio formats by editing the `extensions` array in `Program.cs`.
- To change label text, modify the string passed to `synthesizer.Speak()`.
- For batch or parallel processing, refactor the main loop in `Program.cs`.

---
For questions, review `Program.cs` for all logic and usage patterns. No additional documentation or conventions are present.
