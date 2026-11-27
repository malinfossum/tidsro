# 0007. An .mp3 is decoded to .wav when it is chosen

- Status: Accepted
- Date: 27.11.2026

## Context

Issue #33 asked for .mp3 as well as .wav for My sound. Every sound plays through `SoundPlayer`, which only plays wave audio. The obvious swap was WPF's `MediaPlayer`, which handles both formats. WPF media is built on the Windows Media Player control, and Windows Media Player is an optional Windows feature. On my own machine it is not installed, and `MediaPlayer` fails every file with "Windows Media Player version 10 or later is required". It would also need a temp file for the six built-in chimes, since it cannot play an embedded resource.

## Decision

Tidsro keeps `SoundPlayer` for every sound. An .mp3 is decoded to a 16-bit PCM .wav once, when it is chosen, with the MP3 decoder that ships with Windows (`l3codeca.acm`, through the Audio Compression Manager). The decoded copy is stored under the same fixed name as before. This keeps [0003](0003-my-sound-is-a-copy.md): My sound is still a copy, and there is still no fallback.

## Consequences

- One playback path for all eight sounds. The built-in chimes are untouched, and an alarm plays an .mp3 exactly as it plays a .wav.
- A file Windows cannot decode is refused on screen when it is chosen, never discovered as silence when an alarm fires. Decoding needs no audio device.
- The bytes decide the format, not the extension.
- The decoded copy is larger than the .mp3. An .mp3 is capped at 5 minutes, about 58 MB decoded at most. The copy lives in the Roaming AppData folder with the rest of Tidsro's data, so on a PC with a roaming profile it travels with the profile.
- A sound minutes long has to be stoppable: closing its card, Settings, the edit dialog or the main window ends the playback it started.
- Do not move playback to `MediaPlayer` without first solving the Windows Media Player dependency.
- 0003's last consequence ("Only .wav is supported") no longer holds. The rest of 0003 stands.
