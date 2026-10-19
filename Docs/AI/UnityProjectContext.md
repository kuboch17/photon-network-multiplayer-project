# Unity project context

Focused inspection: football kick repair, 2026-10-19, base commit `db9b5ad`.

- Confirmed Unity 6000.6.2f1; URP 17.6.0, AI Navigation 2.0.14, Test Framework 1.8.0 (`ProjectSettings/ProjectVersion.txt`, `Packages/manifest.json`).
- Legacy Input Manager is active (`activeInputHandler: 0`). Local player movement and actions use `RPGCharacterInputController` and `RPGCharacterController` under `Assets/ExplosiveLLC/RPG Character Animation Pack/Code`. These vendor-directory scripts contain project-specific gameplay modifications.
- Online Hunting uses bundled Photon PUN with first-party logic in `Assets/PvpLab` and `Assets/Photon/RoomManager.cs`. See README for its separate validation flow. Do not infer online support for football from the Hunting implementation.
- Build Settings currently include `menu` then `hunting`. `Assets/Scenes/footballl.unity` is a separate local scene, outside that build flow.
- Football has two active prefab characters, with `playa1` / `playa2` flags, `football: true`, range 2, physical `throwaLeg` poses, and a reference to the same `ball` component. Another inactive character has `football: false`; leave it alone.
- The chicken is the scene object `Ball`. `Assets/Scripts/ball.cs` switches between NavMesh wandering and Rigidbody shots toward `target0` / `target1`. Preserve the existing serialized force of 700 and loft of 0.2 unless runtime evidence warrants tuning.
- Football's current kick keys are N for player 1 and L for player 2 (changed from historical Keypad2 at the user's request). The old code was entirely commented out. Restored input is scoped to football, with a range check, per-player cooldown, input/state gates, and automatic leg reset.
- `tht` temporarily disables `CopyLimb` to pose a physical limb and restores it through `disInit()`. Do not restore the unrelated historical hunting hit/knockback block to repair football.
- Runtime code follows the existing MonoBehaviour architecture, serialized public fields, and `RPGCharacterAnims` namespace. First-party scene scripts generally compile into Assembly-CSharp; Photon and imported packages use their own assemblies.
- Unity CLI is installed, but `unity status` found no Pipeline-connected Editor. The manifest does not include `com.unity.pipeline`. No package changes are required for this repair.
- Unrelated user changes were already present in Hunting, lobby, menu, graphics settings, README and test artifacts at the start. Preserve them.

Validation entry point: `FootballKickVerification.Run` in `Assets/Editor/FootballKickVerification.cs`. Launch the recorded Unity version with `-batchmode -nographics -projectPath <project> -executeMethod FootballKickVerification.Run -logFile Tests/FootballKickVerification.log`, without `-quit`. It opens football for runtime checks without saving the scene. It invokes the kick path directly; physical keyboard input and visual presentation still require an interactive check.
