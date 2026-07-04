# Tidsro for Android

A native Android port of Tidsro: the same countdown timers and Schedule of clock-time and recurring alarms, in the same dark-and-gold look, rebuilt with Kotlin and the Android alarm stack. The desktop app's WPF interface cannot run on Android, so this is a reimplementation that mirrors its rules and behavior rather than a recompile.

## What it does

- **Timers** — presets (5 / 30 / 60 min) or a custom duration (`25`, `5:00`, or `1:30:00`), with an optional label and a per-timer sound. Multiple timers run at once, soonest first; each can be paused, resumed, reset, or cancelled (with undo). Paused timers dim and drop below the active ones. When a timer finishes, a notification offers **+5 min** and **Restart**.
- **Schedule** — clock-time alarms (`14:30`, or shorthand like `9`, `930`, `1430`), one-shot or repeating on a weekday set (Daily, Weekdays, Weekends, or custom days), each with an optional label, per-alarm sound, and an optional **5-minute pre-alarm warning**. Alarms sort by next occurrence, can be switched off without deleting, edited in a dialog, and deleted with undo. When an alarm fires, the notification offers **Snooze +5**.
- Alarms fire exactly (AlarmManager alarm-clock scheduling), survive reboots, and — as on desktop — anything missed while the app was dead fires within a 5-minute grace window on next launch.
- The same six chimes as the desktop app, played through each alarm's notification.
- Everything is stored locally on the device; no accounts, no network.

## Getting the APK

Every push that touches `android/` runs the **Android APK** GitHub Actions workflow, which uploads the built, signed `Tidsro.apk` as a workflow artifact. Open the run on the Actions tab, download the `Tidsro-apk` artifact, unzip it, and open the APK on your phone (you will need to allow installs from your browser or file manager the first time).

## Building locally

Open `android/` in Android Studio, or run:

```
cd android
./gradlew assembleRelease
```

The APK lands in `app/build/outputs/apk/release/`.

## Signing note

Release builds are signed with the checked-in `app/tidsro.keystore` (a self-signed key, password `tidsro-release`). It is deliberately not a secret: it exists so that every CI build is installable and upgrades cleanly over the previous one. Do not reuse it for Play Store distribution; generate a private key for that.
