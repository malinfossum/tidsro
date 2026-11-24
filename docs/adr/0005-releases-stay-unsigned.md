# 0005. Releases stay unsigned

- Status: Accepted
- Date: 27.08.2026

## Context

Windows SmartScreen warns before running `Tidsro-Setup.exe` and `Tidsro.exe`. Code signing looked like the fix. Research showed it is not: since 2024, standard and EV certificates are treated the same, and reputation builds per file over download volume. A newly signed installer still warns until enough clean downloads accumulate, which at Tidsro's scale may be never. The options open to an individual in Norway either cost a yearly fee, issue the certificate in someone else's name, or require moving the release build to CI first.

## Decision

Releases stay unsigned. The README explains the SmartScreen warning and the **More info → Run anyway** step.

## Consequences

- No certificate cost and no change to the hand-run `publish.ps1` release.
- Users see "Unknown publisher" and the SmartScreen warning on every new release.
- If removing the warning ever becomes important, the Microsoft Store is the stronger lever: Store apps are signed by Microsoft and skip SmartScreen. That costs MSIX packaging work, not money. A new ADR would replace this one.
