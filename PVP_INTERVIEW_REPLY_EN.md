Hi, thanks for the follow-up.

**1. Have you worked on real-time PvP combat?**

Yes, at prototype level. In my Unity hunting scene I built a two-player Photon PUN flow in which Player 1 controls the existing `Character1` and Player 2 controls a runtime copy named `Character2`. Each client keeps the original character controller for immediate local movement. The Master Client validates movement proposals, resolves attack-versus-dodge conflicts, owns HP, and republishes the authoritative state.

The concrete problem was that the original melee callbacks were local. Both machines could display a plausible hit or dodge but there was no single owner of the result, so their HP and timing could diverge. I changed the network contract so clients submit intentions and movement proposals rather than damage. The host identifies the player from the Photon sender, rejects duplicate or impossible inputs, checks distance using confirmed positions, and applies damage once. Local animation stays responsive while HP waits for confirmation.

This is honest prototype experience rather than a claim that I shipped a large competitive title. PUN uses a player-hosted Master Client here, so a modified host is still trusted. A production competitive game would move this authority to a dedicated server.

**A. Two players act within 120 ms RTT: dash attack versus dodge**

Assume a symmetric 120 ms RTT between a client and the authority, roughly 60 ms each way, with no unusual jitter.

1. At 0 ms, the attacker presses dash attack. Their client immediately moves the character one metre and plays the attack animation. It sends only the action type and sequence to the host.
2. At roughly 60 ms, the host receives the attack. It checks the sender, sequence, cooldown and HP. It schedules contact 100 ms later.
3. The defender presses dodge at nearly the same time. Their client immediately plays the dodge animation, but it does not grant itself invulnerability or change HP.
4. When the dodge reaches the host, the host creates a 120 ms dodge window. The prototype has a capped 30 ms leniency budget for a close boundary case.
5. At contact time, the host checks the defender's dodge window and the distance between the two host-confirmed positions. If dodge covers contact, the result is `DODGE`. If it does not and distance is at most 2.2 m, the host applies 25 damage. Otherwise the result is `MISS`.
6. The host publishes the result. Both clients keep rendering during the wait. On the next received state, they show the same HP and the remote character continues interpolating toward the confirmed transform.

At 60 FPS, frames are about 16.7 ms apart, but I would not promise an exact display frame from RTT alone. Photon delivery, jitter, the 50 ms state-send interval and render timing can move the visible confirmation. Frame by frame, the attacker sees immediate predicted movement and animation; the defender sees immediate predicted dodge; remote actions appear after network transit; neither client changes HP until the authority confirms the result.

The relevant host-side decision is deliberately small:

```csharp
bool dodged = dodgeFrom[defender] <= contact
    && dodgeUntil[defender] >= contact;

float distance = Vector3.Distance(
    authorityPosition[attacker],
    authorityPosition[defender]);

if (dodged)
    combatResult = "DODGE";
else if (distance <= AttackRange)
    hp[defender] = Mathf.Max(0, hp[defender] - 25);
else
    combatResult = "MISS";
```

Movement follows the same ownership rule. A guest proposes a transform, but the host bounds displacement by elapsed server time before publishing it:

```csharp
float seconds = Mathf.Clamp((now - lastMoveAt[slot]) / 1000f, .01f, .25f);
float allowed = MaximumSpeed * seconds + .75f;

Vector3 delta = proposed - authorityPosition[slot];
if (delta.magnitude > allowed)
    proposed = authorityPosition[slot] + delta.normalized * allowed;
```

**B. Worst exploit from hit leniency or cancel windows**

The worst exploit would be a client waiting to learn that it was hit and then sending a backdated dodge, or repeatedly cancelling attacks into fresh invulnerability. A related exploit is teleporting into range and claiming damage.

I mitigate that by deriving identity from `photonEvent.Sender`, requiring monotonically increasing sequences, enforcing cooldown on the host, calculating range from host-confirmed positions, and never accepting client-provided damage. The client does not choose an arbitrary action timestamp. Dodge leniency is a fixed 30 ms budget, so it cannot grow with a forged latency claim. Once the host resolves contact, a later dodge cannot undo the HP change.

```csharp
if (slot != SlotForActor(sender) ||
    seq <= lastActionSequence[slot] ||
    now < readyAt[slot] || hp[slot] <= 0)
{
    rejectedInputs++;
    return;
}
```

To preserve game feel, I predict presentation, not authority: movement and animation begin locally, while damage waits for the host. I would tune the small leniency value with playtests and latency telemetry. For a dodge-focused competitive game, I generally find “I clearly dodged but still took damage” more frustrating than an occasional unconfirmed hit for the attacker, but unlimited defender favour creates abuse and makes offense feel random. A small, measured window plus clear hit feedback is the compromise I used here.

I validated the deterministic edge cases and wire rules with 41 automated checks, including exact contact timing, late dodge, duplicate sequences, cooldown, range boundaries, state snapshots and movement limits. I also keep the limitation explicit: visual quality, camera handoff and feel still require a two-client playtest, and a production version would need dedicated-server authority, better reconciliation, network-condition testing and stronger cheat resistance.
