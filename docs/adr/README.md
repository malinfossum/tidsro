# Architecture decisions

Short records of the decisions in Tidsro that are hard or expensive to reverse. Each file says what was decided, why, and what it costs. The code shows how things work today; these files keep the reasons, so nobody (person or agent) re-proposes something that was already weighed and turned down.

A record is never rewritten. If a decision changes, a new record replaces it and the old one gets `Status: Superseded by NNNN`.

| # | Decision | Date |
|---|----------|------|
| [0001](0001-one-json-data-file.md) | All data lives in one JSON file with a schema version | 17.06.2026 |
| [0002](0002-fire-once-without-a-ledger.md) | Each alarm fires once, without a "last fired" log | 18.06.2026 |
| [0003](0003-my-sound-is-a-copy.md) | My sound is a copy, with no silent fallback | 10.09.2026 |
| [0004](0004-no-third-party-sdks.md) | No third-party SDKs, telemetry or monetization | 18.11.2026 |
| [0005](0005-releases-stay-unsigned.md) | Releases stay unsigned | 27.08.2026 |
| [0006](0006-week-rows-are-occupied-times.md) | Week rows are occupied times only | 03.09.2026 |
| [0007](0007-mp3-is-decoded-when-chosen.md) | An .mp3 is decoded to .wav when it is chosen | 27.11.2026 |

## Template

```markdown
# NNNN. Title

- Status: Accepted
- Date: DD.MM.YYYY

## Context
What forced a choice, and what the options were.

## Decision
What I chose, in one or two sentences.

## Consequences
What gets easier, what gets harder, and what not to do because of it.
```

Words like *card*, *heads-up* and *fire* are defined in the [glossary](../glossary.md).
