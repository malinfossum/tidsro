# 0002. Each alarm fires once, without a "last fired" log

- Status: Accepted
- Date: 18.06.2026 (recurring alarms, v1.3.0)

## Context

An alarm must fire exactly once per occurrence, even across sleep, a relaunch or two scheduler ticks that land close together. The usual fix is a ledger: store when each alarm last fired and check it before firing. That is one more piece of saved state that can drift from the alarm it describes.

## Decision

The alarm's own state is the record. Before the `Fired` event goes out, a one-shot alarm is removed from the armed set and marked `Fired`, and a recurring alarm moves its `EndsAt` forward to the next occurrence. A tick that comes after that has nothing left to fire. The card gets a snapshot copy of the alarm, so its buttons can never change the live alarm. A missed alarm still fires within a 5-minute grace after sleep or a relaunch, and expires quietly after it.

## Consequences

- No `LastFired` field, and no migration for one.
- Fire timing is tested in `SchedulerService` alone, with a fake clock.
- An alarm with an end time chimes at its start only. A second chime at the end would need the per-occurrence ledger this design avoids, because the next occurrence has already been set by then. The README lists this as a deliberate omission.
