# ANIMATION SYNC - FULL CHANGE SPECIFICATION

## 1. OVERVIEW

```text
BEFORE:
  Host -> PlayAnimPacket -> Client
  If the packet drops, the client can stay on the wrong animation forever.

AFTER:
  Minions:
    Host -> PlayAnimPacket -> Client        (event-driven, unchanged)
    Host -> DuplicantStatePacket -> Client  every 200ms

  Critters:
    Host -> PlayAnimPacket -> Client        (event-driven once they have NetId)
    Host -> AnimSyncPacket -> Client        every 1000ms

  Animated buildings:
    Host -> AnimSyncPacket -> Client        every 1000ms
```

This change stays inside the author's current state-sync model. It adds periodic animation reconciliation on top of existing event-driven packets. It does not touch the transport layer, does not change `BuildingSyncer`, and does not revive `DuplicantClientController` or `NavigatorTransitionPacket`.

## 2. SCOPE MATRIX

| Entity type | Event-driven packet | Periodic packet | Interval |
|-------------|---------------------|-----------------|----------|
| Minions | `PlayAnimPacket` | `DuplicantStatePacket` | 200ms |
| Critters | `PlayAnimPacket` | `AnimSyncPacket` | 1000ms |
| Animated buildings | none | `AnimSyncPacket` | 1000ms |

Only entities with both `NetworkIdentity` and `KBatchedAnimController` participate. Buildings are included only when they are animated and network-identifiable.

## 3. PACKET CHANGES

### `DuplicantStatePacket` (minions, every 200ms)

`DuplicantStatePacket` keeps its existing high-level duplicant state fields and now also carries:

- `AnimPlayMode` (`int`)
- `AnimSpeed` (`float`)

Minion reconciliation uses:

- `CurrentAnimName`
- `AnimElapsedTime`
- `AnimPlayMode`
- `AnimSpeed`

This branch keeps the current packet-shape change as-is. Host and clients must run the same mod version. No handshake/version-gating is added in this increment.

### `AnimSyncPacket` (critters and animated buildings, every 1000ms)

`AnimSyncPacket` is a compact periodic reconciliation packet:

- `NetId` (`int`)
- `AnimHash` (`int`)
- `Mode` (`byte`)
- `Speed` (`float`)
- `ElapsedTime` (`float`)

The packet implements `IBulkablePacket` and is emitted by a new host-side `AnimStateSyncer` component attached only to eligible non-minion animated entities.

## 4. RECONCILIATION RULES

Both packet paths use the same reconciliation policy:

1. If the client's current animation differs from the authoritative animation, replay the authoritative animation with the packet's mode and speed, then snap elapsed time.
2. If the animation matches but elapsed-time drift exceeds 150ms, snap elapsed time.
3. If the animation matches and drift is within 150ms, do nothing.

`Mode` and `Speed` are authoritative replay parameters in this increment. They are not treated as standalone mismatch triggers when the current animation already matches.

## 5. ARCHITECTURE FIT

- This is state reconciliation, not lockstep.
- The host remains authoritative.
- The transport layer stays untouched.
- `BuildingSyncer` keeps its existing 30s full-state building reconciliation behavior.
- `PlayAnimPacket` stays event-driven and unchanged as a class.
- `DuplicantClientController` remains commented out.
- `NavigatorTransitionPacket` remains commented out.

## 6. FILES IN THIS CHANGE

- `DuplicantStatePacket.cs`
  - Uses shared reconciliation logic for minions.
  - Keeps `AnimPlayMode` and `AnimSpeed`.
- `DuplicantStateSender.cs`
  - Continues to populate minion animation state every 200ms.
- `AnimReconciliationHelper.cs`
  - Resolves elapsed-time setters once.
  - Applies shared replay/drift correction logic.
- `AnimSyncPacket.cs`
  - New periodic non-minion animation reconciliation packet.
- `AnimStateSyncer.cs`
  - New host-side sender for critters and animated buildings.
- `EntityTemplatesPatch.cs`
  - Gives animated critters `NetworkIdentity` and `AnimStateSyncer`.
- `BuildingSpawnPatch.cs`
  - Gives animated buildings `NetworkIdentity` and `AnimStateSyncer`.
- `AnimSyncTests.cs`
  - Covers reconciliation helper behavior, packet roundtrip, and non-minion sync eligibility.

## 7. KNOWN LIMITATIONS

- Transition-driver position offsets remain out of scope.
- This increment does not restore client-side transition playback controllers.
- If reflection-based elapsed-time restore fails, the client falls back to current event-driven behavior.
- Buildings use periodic reconciliation only in this increment. Immediate event-driven building animation sync is not added here.

## 8. ACCEPTANCE

- Minion wrong-animation desync self-heals within one 200ms heartbeat plus render delay.
- Critter wrong-animation desync self-heals within one 1000ms sync interval.
- Animated-building wrong-animation desync self-heals within one 1000ms sync interval.
- Drift-only desync snaps elapsed time without replaying a different animation.
- Entities without `NetworkIdentity` or `KBatchedAnimController` fail closed with no crash.
- Join-in-progress clients converge without re-enabling transition playback code.
