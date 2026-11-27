# 0003. My sound is a copy, with no silent fallback

- Status: Accepted. The last consequence is superseded by [0007](0007-mp3-is-decoded-when-chosen.md)
- Date: 10.09.2026 (v2.6.0)

## Context

Users wanted their own alarm sound. The first idea was to remember the path to the .wav the user picked, and fall back to a built-in chime if the file went missing.

## Decision

Tidsro copies the chosen .wav into a folder it controls, under one fixed file name. The name the user recognizes is stored separately as a label (`AppSettings.CustomSoundName`). There is one custom slot. If the copy is missing, the alarm plays no sound, and Tidsro shows that no sound is chosen. It never swaps in a chime the user did not choose.

## Consequences

- Moving, renaming or deleting the original file, or unplugging the USB stick it came from, cannot silence an alarm.
- Nothing a user types or an imported backup carries can steer a file path, because the stored copy always has the same name.
- Very large files are refused instead of copied into AppData.
- Only .wav is supported, because of how sounds are played today. MP3 support is open as issue #33.
