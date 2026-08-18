# Third-party notices

Tidsro itself is licensed under Apache-2.0 (see `LICENSE`). It bundles the following
third-party components, which are licensed separately.

## IBM Plex Sans, IBM Plex Mono

Copyright © IBM Corp. Licensed under the SIL Open Font License, Version 1.1, with
Reserved Font Name "Plex".

The copyright year differs between the bundled files, so no single year is stated above.
`OFL.txt` as bundled reads "Copyright © 2017 IBM Corp."; the name table inside
`IBMPlexMono-Regular.ttf` and `IBMPlexMono-Medium.ttf` reads 2017, and the one inside
`IBMPlexSans-Regular.ttf` and `IBMPlexSans-SemiBold.ttf` reads 2018. All four are shipped
unmodified, so their own declarations stand as written.

The four font files are embedded, unmodified, in the application binary. `OFL.txt` is
embedded alongside them, so both distribution channels carry the licence:

- **Installer** — `OFL.txt` is also written beside the application as `OFL-IBMPlex.txt`
  (see `installer/Tidsro.iss`).
- **Portable `Tidsro.exe`** — travels with no companion files, so it carries the licence
  inside itself. **Settings ▸ View font licence** reads the embedded `OFL.txt` and shows
  it in full.

Settings also shows a one-line acknowledgement ("Typeface: IBM Plex, © IBM Corp., SIL Open
Font License 1.1") next to that button.

Source in this repository: `src/Tidsro/Assets/fonts/`.

Licence: <https://openfontlicense.org>
Source: <https://github.com/IBM/plex>
