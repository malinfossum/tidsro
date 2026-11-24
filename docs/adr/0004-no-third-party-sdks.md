# 0004. No third-party SDKs, telemetry or monetization

- Status: Accepted
- Date: 18.11.2026

## Context

Once Tidsro was public, an SDK vendor offered to pay per installed device for running its code inside the app. Offers like that (ads, bandwidth sharing, analytics) will keep coming for any free Windows app with users.

## Decision

Tidsro ships no third-party SDKs that collect data, phone home or make money from its users. No telemetry, no ads, no data sharing.

## Consequences

- Every future offer of this kind gets the same answer, without a new debate.
- "Local-first: no accounts, no network" in the README stays true.
- A library that runs locally and sends nothing (like the tray icon library) is fine, and is judged like any other dependency.
