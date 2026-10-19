# Football kick verification — 2026-10-19

Scene: `Assets/Scenes/footballl.unity`, Unity 6000.6.2f1, Windows.

## Findings and change

The historical N / Keypad2 input blocks in `RPGCharacterController.Update` were commented out. The two active football characters already have `football` enabled, range 2, the shared chicken-ball reference, and physical kick poses assigned. The other character with football disabled is inactive and was left untouched.

Restored the original keys and goal selection (player 1 -> target1, player 2 -> target0), a range-limited shot, physical leg pose, automatic pose release and a 0.6-second cooldown. Respect disabled input, dead/blocking state and action locks. No scenes, prefabs, ball force settings, hunting logic or input mappings were changed.

## Executed validation

- Full Assembly-CSharp source compilation using the imported Unity Editor response file, installed Unity 6000.6.2f1 compiler and project dependencies: PASS, exit 0 (`FootballCompile.log`). Existing unrelated obsolete-API, unused-field and serialization warnings remain.
- Editor verification code compiled against the imported editor dependencies: PASS (`FootballEditorCompile.log`).
- Reviewed scene/prefab references, the two player slots, historical key mapping, and the final source diff.
- Runtime verification was attempted in a separate batchmode Editor, but Unity exited before loading the project. A process check confirmed the project is already open in another Editor with football visible. That Editor has no Pipeline connection. Runtime checks did not execute, and the existing Editor was left open. `FootballKickVerification.log` records the failed launch, not passing gameplay tests.

## Repeatable runtime check

After closing the interactive Editor, run Unity with:

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.2f1/Editor/Unity.exe' -batchmode -nographics -projectPath (Get-Location).Path -executeMethod FootballKickVerification.Run -logFile Tests/FootballKickVerification.log
```

Do not add `-quit`; the verification exits after its Play Mode checks. Expected final marker: `FOOTBALL VERIFICATION EXIT 0`. It checks both player slots, common ball, leg references, out-of-range rejection, both goal directions, cooldown, repeated kicks, navigation resumption and pose reset. It invokes the kick method directly, so it does not establish physical keyboard or visual correctness.

Manual acceptance: open footballl, press Play and focus Game. Bring player 1 within 2 units of the chicken and press N; then use player 2 and press L (updated from Keypad2 at the user's request). Each should kick toward the opposite goal, with the leg returning automatically. Repeat after 0.6 seconds, try out of range, and confirm the chicken resumes navigation after the shot slows. Stop Play Mode without saving runtime changes.
