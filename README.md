# voice-chapter

A .NET command-line tool that batch-processes audio files in a folder and prepends a spoken label (based on the file name) to each file.

The tool can use either the built-in Windows `System.Speech` engine or the cross-platform [Piper](https://github.com/rhasspy/piper) TTS via PiperSharp, and it uses `ffmpeg` under the hood to concatenate the generated label with the original audio.

---

## Installation

### Prerequisites

- .NET 10.0+
- `ffmpeg` (optional)
  - If `ffmpeg` is not found/provided, the tool will download a compatible binary automatically.

### Install as a global .NET tool

Once the package is published to NuGet.org (with package ID `voice-chapter`), install it as a global tool:

```bash
dotnet tool install -g voice-chapter
```

To update to the latest version:

```bash
dotnet tool update -g voice-chapter
```

After installation, the command `voice-chapter` will be available on your PATH.

---

## Basic Usage

The simplest way to run the tool is to point it at a folder containing audio files:
```bash
voice-chapter --folder "C:\path\to\audio"
```

This will:

- Find all supported audio files in the folder (`.wav`, `.mp3`, `.flac`, `.m4a`, `.ogg`, `.aac`).
- For each file, generate a spoken label from the file name (without extension).
- Prepend that label to the audio using `ffmpeg`.
- Write a new file named `<original>_labeled.<ext>` next to the original.

Example:

- Input: `chapter01.mp3`
- Output: `chapter01_labeled.mp3` (starts with a voice saying "chapter01", then plays the original audio).

---

## Command-line Options

The root command is defined as:

```csharp
voice-chapter --folder <string> \
              [--ffmpeg <string>] \
              [--provider <Speech|Piper>] \
              [--model-key <string>] \
              [--transliterate <true|false>]
```

In detail:

### `--folder` (required)

**Short alias:** `-f`

Path to the folder containing audio files to process.

Example:

```bash
voice-chapter --folder "C:\Audiobooks\Book1"
```

### `--ffmpeg` (optional)

**Short alias:** `-fe`

Path to the `ffmpeg` executable or to a folder containing `ffmpeg`.

- If omitted, the tool will automatically download ffmpeg binaries into a local `ffmpeg-binaries` folder and use them.
- If you already have ffmpeg installed and on your PATH, you can usually skip this parameter.

Examples:

```bash
# Explicit ffmpeg.exe path
voice-chapter --folder "C:\Audiobooks\Book1" --ffmpeg "C:\Tools\ffmpeg\ffmpeg.exe"

# Folder that contains ffmpeg
voice-chapter --folder "C:\Audiobooks\Book1" --ffmpeg "C:\Tools\ffmpeg"
```

### `--provider` (optional)

**Short alias:** `-p`

Choose which TTS engine to use for generating spoken labels:

- `Speech` (default) – uses Windows `System.Speech.Synthesis.SpeechSynthesizer`.
- `Piper` – uses Piper via PiperSharp (cross-platform, but requires downloading a voice model).

Examples:

```bash
# Default (System.Speech)
voice-chapter --folder "C:\Audiobooks\Book1"

# Use Piper TTS
voice-chapter --folder "C:\Audiobooks\Book1" --provider Piper
```

### `--model-key` (optional, Piper only)

**Short alias:** `-m`

Specifies the Piper voice model key when `--provider Piper` is chosen.

- Default: `en_GB-alan-medium`
- On first use, the tool will download the specified model if it is not already present.

Example:

```bash
voice-chapter --folder "C:\Audiobooks\Book1" \
              --provider Piper \
              --model-key en_GB-alan-medium
```

### `--transliterate` (optional)

**Short alias:** `-tr`

Controls whether to transliterate file names before sending them to TTS.

- `false` (default) – use the raw file name (without extension) as the label.
- `true` – transliterate from Latin to Cyrillic before generating the spoken label.

Example:

```bash
# Enable transliteration
voice-chapter --folder "C:\Audiobooks\Book1" --transliterate true
```

> Note: Transliteration is useful if your file names are in Latin script but you want the spoken labels to use Cyrillic.

---

## Notes and Limitations

- **Supported audio formats:** `.wav`, `.mp3`, `.flac`, `.m4a`, `.ogg`, `.aac`.
- **Output files:** Each processed file produces a new file named `<original>_labeled<ext>` in the same folder.
- **Windows-specific TTS:** The `Speech` provider requires Windows (`System.Speech.Synthesis`). Piper can be used on other platforms.
- **ffmpeg requirement:** The tool relies on ffmpeg for audio concatenation. If not provided, it will attempt to download compatible binaries.
