# 0001. All data lives in one JSON file with a schema version

- Status: Accepted
- Date: 17.06.2026 (format introduced in v1.1.0)

## Context

Tidsro is local-first: no accounts and no network. It needs to keep settings and alarms between runs, survive a crash in the middle of a save, and keep reading files written by older versions after an update.

## Decision

Everything lives in one file, `%AppData%\Tidsro\data.json`, as a `TidsroData` document with a `SchemaVersion`. A save writes a temp file and swaps it in with `File.Replace`, so a crash never leaves a half-written file. A file that fails to load is copied to `data.json.corrupt`, and Tidsro starts with defaults instead of refusing to launch. The copy is deleted after the next good save.

## Consequences

- Every format change bumps `TidsroData.CurrentSchema`, and the loader keeps reading every older version. A v1.0 file (bare settings, no alarms) still loads today.
- Export and import reuse the same format, so a backup is this document in a file the user picked.
- The live schedule is in memory between saves. Tidsro saves when alarms change and on exit, so closing it from the tray is safe. Force-killing it can lose an edit that has not been saved yet.
- No database. One file is enough for one person's alarms, and it stays readable by hand.
