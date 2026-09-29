using System;
using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace PvpLab
{
    public sealed class PunCombatSession : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        private HuntingCombatDemo presentation;
        private CombatSimulation authority;
        private PunCombatProtocol.InputGate inputGate;
        private PunCombatProtocol.Snapshot latest;
        private string match, round, message = "Waiting for both players to load hunting...";
        private readonly int[] actors = new int[2];
        private readonly float[] displayX = new float[2];
        private int slot, hostActor, startTimestamp, sequence, mode, lastSnapshot = -1;
        private int localActionReady, lastMove, nextMoveAt, lastActionSequence;
        private float lastSnapshotReceived, nextPublish, lastRoundRequest, ackMs;
        private bool ready, aborted, scriptedActionSent, probeSent, stale;
        private bool smokeRoundLogged;
        private PendingAction pending;
        private sealed class PendingAction
        {
            public int Sequence, Start, X, Facing;
            public float SentAt;
            public CombatSimulation.ActionKind Kind;
        }
        private int Now { get { return PunCombatProtocol.Elapsed(PhotonNetwork.ServerTimestamp, startTimestamp); } }
        private IEnumerator Start()
        {
            if (!PhotonNetwork.InRoom) { enabled = false; yield break; }
            var properties = PhotonNetwork.CurrentRoom.CustomProperties;
            if (!(properties[PunCombatProtocol.MatchKey] is string) || !(properties[PunCombatProtocol.PlayerOneKey] is int) ||
                !(properties[PunCombatProtocol.PlayerTwoKey] is int))
            { Abort("Missing match setup. Start the duel from PvPLobby."); yield break; }
            match = (string)properties[PunCombatProtocol.MatchKey];
            actors[0] = (int)properties[PunCombatProtocol.PlayerOneKey]; actors[1] = (int)properties[PunCombatProtocol.PlayerTwoKey];
            hostActor = actors[0]; slot = SlotForActor(PhotonNetwork.LocalPlayer.ActorNumber);
            if (slot < 0 || PhotonNetwork.MasterClient.ActorNumber != hostActor || actors[0] == actors[1])
            { Abort("Player assignment changed. Please create a new room."); yield break; }
            presentation = GetComponent<HuntingCombatDemo>();
            yield return presentation.PrepareOnline();
            if (!presentation.IsPrepared) { Abort("Could not prepare hunting characters: " + presentation.PreparationError); yield break; }
            ready = true;
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { { PunCombatProtocol.ReadyKey, match } });
            lastSnapshotReceived = Time.realtimeSinceStartup;
        }
        private int SlotForActor(int actor) { return actor == actors[0] ? 0 : actor == actors[1] ? 1 : -1; }
        private bool BothReady()
        {
            if (!ready || PhotonNetwork.CurrentRoom.PlayerCount != 2) return false;
            foreach (Player player in PhotonNetwork.PlayerList)
                if (!(player.CustomProperties[PunCombatProtocol.ReadyKey] is string) ||
                    (string)player.CustomProperties[PunCombatProtocol.ReadyKey] != match) return false;
            return true;
        }
        private void StartRound(int nextMode)
        {
            if (!PhotonNetwork.IsMasterClient || !BothReady() || Time.realtimeSinceStartup < lastRoundRequest + 1) return;
            lastRoundRequest = Time.realtimeSinceStartup;
            string epoch = Guid.NewGuid().ToString("N");
            int start = unchecked(PhotonNetwork.ServerTimestamp + 1000);
            object[] payload = { match, epoch, start, nextMode };
            if (!PhotonNetwork.RaiseEvent(PunCombatProtocol.RoundEvent, payload,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable))
            { Abort("Could not send round setup."); return; }
            ApplyRound(epoch, start, nextMode);
        }
        private void ApplyRound(string epoch, int start, int nextMode)
        {
            round = epoch; startTimestamp = start; mode = nextMode;
            sequence = 0; lastSnapshot = -1; pending = null; localActionReady = 0;
            lastMove = 0; nextMoveAt = 0; lastActionSequence = 0;
            scriptedActionSent = probeSent = false; stale = false;
            smokeRoundLogged = false;
            var initial = new CombatSimulation(true, mode == -2 ? 5000 : 2200);
            initial.AdvanceTo(0);
            inputGate = new PunCombatProtocol.InputGate();
            authority = PhotonNetwork.IsMasterClient ? initial : null;
            PunCombatProtocol.TrySnapshot(PunCombatProtocol.Encode(round, initial, 0), round, -1, out latest);
            for (int i = 0; i < 2; i++) displayX[i] = latest.Fighters[i].X;
            lastSnapshotReceived = Time.realtimeSinceStartup; nextPublish = 0;
            presentation.ResetOnlineVisuals();
            message = nextMode == -1 ? "Live duel. A/D move, J dash, K dodge." : "Network timing test: both computers send real inputs. Timing depends on the connection.";
        }
        public void OnEvent(EventData photonEvent)
        {
            if (!ready || aborted || !PhotonNetwork.InRoom) return;
            if (photonEvent.Code == PunCombatProtocol.RoundEvent)
            {
                // Clients may not reset rounds or invent authoritative states.
                if (photonEvent.Sender != hostActor || PhotonNetwork.MasterClient.ActorNumber != hostActor) return;
                var data = photonEvent.CustomData as object[];
                if (data == null || data.Length != 4 || !(data[0] is string) || (string)data[0] != match ||
                    !(data[1] is string) || ((string)data[1]).Length != 32 || !(data[2] is int) || !(data[3] is int)) return;
                int nextMode = (int)data[3];
                if (nextMode != -4 && nextMode != -2 && nextMode != -1 && nextMode != 10 && nextMode != 100 && nextMode != 110) return;
                if ((string)data[1] != round) ApplyRound((string)data[1], (int)data[2], nextMode);
            }
            else if (photonEvent.Code == PunCombatProtocol.InputEvent)
            {
                if (!PhotonNetwork.IsMasterClient || authority == null || Now < 0 || Finished()) return;
                int senderSlot = SlotForActor(photonEvent.Sender), seq;
                CombatSimulation.ActionKind kind;
                if (!PunCombatProtocol.TryInput(photonEvent.CustomData, round, out seq, out kind)) return;
                int receivedAt = Math.Max(authority.TimeMs + 1, Now);
                if (!inputGate.Accept(senderSlot, seq, receivedAt)) return;
                // Sender identity comes from Photon. Receipt time comes from the host.
                // Payload contains neither actor number, position, damage nor timestamp.
                authority.Enqueue(senderSlot, seq, kind, receivedAt);
            }
            else if (photonEvent.Code == PunCombatProtocol.SnapshotEvent)
            {
                if (PhotonNetwork.IsMasterClient || photonEvent.Sender != hostActor || round == null) return;
                AcceptSnapshot(photonEvent.CustomData);
            }
        }
        private void AcceptSnapshot(object payload)
        {
            PunCombatProtocol.Snapshot snapshot;
            if (!PunCombatProtocol.TrySnapshot(payload, round, lastSnapshot, out snapshot)) return;
            latest = snapshot; lastSnapshot = snapshot.Tick;
            lastSnapshotReceived = Time.realtimeSinceStartup; stale = false;
            if (pending != null && snapshot.Fighters[slot].LastSequence >= pending.Sequence)
            {
                ackMs = (Time.realtimeSinceStartup - pending.SentAt) * 1000;
                if (snapshot.Fighters[slot].ActionSequence != pending.Sequence)
                {
                    message = "Authority rejected the predicted action (cooldown/recovery/state).";
                    presentation.ResetOnlineVisuals();
                }
                localActionReady = snapshot.Fighters[slot].ReadyAt;
                pending = null;
            }
        }
        private bool SendRaw(int seq, CombatSimulation.ActionKind kind)
        {
            return PhotonNetwork.RaiseEvent(PunCombatProtocol.InputEvent, new object[] { round, seq, (byte)kind },
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }
        private void SendAction(CombatSimulation.ActionKind kind)
        {
            if (pending != null || Now < localActionReady || latest.Fighters[slot].Hp <= 0) return;
            int seq = ++sequence;
            if (!SendRaw(seq, kind)) { message = "Input could not be sent."; return; }
            lastActionSequence = seq;
            pending = new PendingAction { Sequence = seq, Start = Now, SentAt = Time.realtimeSinceStartup, Kind = kind,
                X = Mathf.RoundToInt(displayX[slot]), Facing = displayX[1 - slot] >= displayX[slot] ? 1 : -1 };
            localActionReady = Now + CombatSimulation.CooldownMs;
        }
        private void SendMove(int direction)
        {
            if (direction == lastMove && Now < nextMoveAt) return;
            var kind = direction < 0 ? CombatSimulation.ActionKind.MoveLeft : direction > 0 ? CombatSimulation.ActionKind.MoveRight : CombatSimulation.ActionKind.Stop;
            if (SendRaw(++sequence, kind)) { lastMove = direction; nextMoveAt = Now + 100; }
        }
        private void SendProbe()
        {
            if (lastActionSequence <= 0) return;
            SendRaw(lastActionSequence, CombatSimulation.ActionKind.DashAttack); // Intentional replay.
            SendRaw(++sequence, CombatSimulation.ActionKind.Dodge); // Intentional recovery cancel.
        }
        private bool Finished()
        {
            var fighters = authority != null ? authority.Fighters : latest.Fighters;
            return fighters[0].Hp <= 0 || fighters[1].Hp <= 0;
        }
        private void Update()
        {
            if (!ready || aborted || !PhotonNetwork.InRoom) return;
            if (PhotonNetwork.MasterClient.ActorNumber != hostActor || PhotonNetwork.CurrentRoom.PlayerCount != 2)
            { Abort("A player or the host left. The duel was stopped."); return; }
            if (round == null)
            {
                if (PhotonNetwork.IsMasterClient && BothReady()) StartRound(PunSmoke.Enabled ? -2 : -1);
                return;
            }
            int now = Now;
            if (authority != null && now >= 0)
            {
                if (!Finished() && now - authority.TimeMs > 2000) { Abort("The host paused too long. Start a fresh match."); return; }
                if (!Finished()) authority.AdvanceTo(now);
                if (Time.realtimeSinceStartup >= nextPublish)
                {
                    var packet = PunCombatProtocol.Encode(round, authority, inputGate.Rejected);
                    // Include monotonically advancing ticks after KO so periodic snapshots still heal packet loss.
                    if (Finished()) packet[1] = (now / CombatSimulation.TickMs) * CombatSimulation.TickMs;
                    AcceptSnapshot(packet);
                    PhotonNetwork.RaiseEvent(PunCombatProtocol.SnapshotEvent, packet,
                        new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
                    nextPublish = Time.realtimeSinceStartup + .05f;
                }
            }
            stale = !PhotonNetwork.IsMasterClient && Time.realtimeSinceStartup - lastSnapshotReceived > 2;
            int direction = 0;
            if (now >= 0 && !Finished() && !stale && Application.isFocused)
            {
                if (mode == -1)
                {
                    direction = (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0);
                    if (Input.GetKeyDown(KeyCode.J)) SendAction(CombatSimulation.ActionKind.DashAttack);
                    if (Input.GetKeyDown(KeyCode.K)) SendAction(CombatSimulation.ActionKind.Dodge);
                    SendMove(direction);
                }
            }
            // Automated tests also run when one of two local test windows lacks focus.
            if (now >= 0 && mode != -1 && !Finished() && !stale)
            {
                int at = 200 + (slot == 1 ? mode : 0);
                if (!scriptedActionSent && now >= at && (slot == 0 || mode >= 0))
                {
                    scriptedActionSent = true;
                    SendAction(slot == 0 ? CombatSimulation.ActionKind.DashAttack : CombatSimulation.ActionKind.Dodge);
                }
                if (mode == -4 && slot == 0 && !probeSent && now >= 220) { probeSent = true; SendProbe(); }
            }
            Render(direction, Mathf.Max(0, now));
            if (PunSmoke.Enabled) CheckSmokeRound();
        }
        private void CheckSmokeRound()
        {
            if (latest.ResolvedAt < 0 || Now < latest.ResolvedAt + 800) return;
            if (!smokeRoundLogged)
            {
                smokeRoundLogged = true;
                if (mode == 10 && !string.IsNullOrEmpty(PunSmoke.Screenshot))
                    ScreenCapture.CaptureScreenshot(PunSmoke.Screenshot);
                Debug.Log("PUN-SMOKE RESULT " + PunSmoke.Role + " round=" + round + " mode=" + mode +
                    " contact=" + latest.ResolvedAt + " hp=" + latest.Fighters[0].Hp + "," + latest.Fighters[1].Hp +
                    " rejected=" + latest.Rejected + " result=" + latest.Result);
                if (mode == -2 && (latest.Fighters[1].Hp != 100 || !latest.Result.StartsWith("MISS")))
                    PunSmoke.Finish(false, "Range check failed");
                if (mode == -4 && (latest.Fighters[1].Hp != 75 || latest.Rejected < 2))
                    PunSmoke.Finish(false, "Replay/cancel check failed");
            }
            if (PunSmoke.Done || Now < latest.ResolvedAt + 1600) return;
            if (mode == 10) { PunSmoke.Finish(true, "Three online rounds completed"); return; }
            if (PhotonNetwork.IsMasterClient) StartRound(mode == -2 ? -4 : 10);
        }
        private void Render(int direction, int now)
        {
            var visual = new[] { latest.Fighters[0].Copy(), latest.Fighters[1].Copy() };
            float dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            for (int i = 0; i < 2; i++)
            {
                var fighter = visual[i];
                float desired = fighter.X;
                int age = now - fighter.AttackAt;
                if (!stale && age >= 0 && age <= CombatSimulation.WindupMs)
                    desired = fighter.AttackOrigin + fighter.Facing * CombatSimulation.DashDistance * age / CombatSimulation.WindupMs;
                else if (!stale && now >= fighter.BusyUntil && now < fighter.MoveUntil && fighter.Hp > 0)
                    desired += fighter.MoveDirection * Mathf.Clamp(now - latest.Tick, 0, 100) * 3;
                if (i == slot && mode == -1 && !stale && fighter.Hp > 0 && now >= fighter.BusyUntil && pending == null)
                    desired = fighter.X + direction * Mathf.Clamp(now - latest.Tick + 20, 0, 150) * 3;
                if (i == slot && pending != null && !stale)
                {
                    fighter.ActionSequence = pending.Sequence; fighter.Action = pending.Kind; fighter.Facing = pending.Facing;
                    if (pending.Kind == CombatSimulation.ActionKind.DashAttack)
                        desired = pending.X + pending.Facing * CombatSimulation.DashDistance * Mathf.Clamp01((now - pending.Start) / (float)CombatSimulation.WindupMs);
                    else fighter.DodgeAt = pending.Start;
                }
                desired = Mathf.Clamp(desired, -6000, 8000);
                displayX[i] = Mathf.Lerp(displayX[i], desired, 1 - Mathf.Exp(-25 * dt));
                fighter.X = Mathf.RoundToInt(displayX[i]);
            }
            presentation.RenderOnline(visual, now, dt);
        }
        private void Abort(string reason)
        {
            if (aborted) return;
            if (PunSmoke.Enabled && !PunSmoke.Done)
                PunSmoke.Finish(mode == 10 && smokeRoundLogged, reason);
            aborted = true; message = reason;
            if (PunLobby.Instance != null) PunLobby.Instance.ReturnToLobby(reason);
            else
            {
                PhotonNetwork.AutomaticallySyncScene = false;
                PhotonNetwork.Disconnect();
                UnityEngine.SceneManagement.SceneManager.LoadScene(PunLobby.LobbyScene);
            }
        }
        public override void OnPlayerLeftRoom(Player otherPlayer) { Abort("Opponent disconnected. Create or join a new room."); }
        public override void OnMasterClientSwitched(Player newMasterClient) { Abort("Host disconnected. This prototype ends the match instead of migrating authority."); }
        public override void OnDisconnected(DisconnectCause cause) { aborted = true; }
        private void OnGUI()
        {
            float scale = Mathf.Min(Screen.width / 1100f, Screen.height / 760f);
            Matrix4x4 previous = GUI.matrix; GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(15, 15, 1070, 285), GUI.skin.box);
            GUILayout.Label("HUNTING / PHOTON PUN / " + (PhotonNetwork.IsMasterClient ? "HOST AUTHORITY" : "REMOTE CLIENT"));
            GUILayout.Label(message);
            if (latest != null)
            {
                GUILayout.Label("You: P" + (slot + 1) + " | Photon ping " + PhotonNetwork.GetPing() + " ms | last action acknowledgement " + ackMs.ToString("F0") + " ms");
                GUILayout.Label("P1 HP " + latest.Fighters[0].Hp + "   |   P2 HP " + latest.Fighters[1].Hp + "   |   " + latest.Result);
                GUILayout.Label("Confirmed tick " + latest.Tick + " ms | contact " + latest.ResolvedAt + " ms | rejected inputs " + latest.Rejected);
                GUILayout.Label("A/D move | J dash attack | K dodge. HP changes only on authoritative confirmation.");
                if (Now < 0) GUILayout.Label("Round starts in " + (-Now / 1000f).ToString("F1") + " s");
                if (stale) GUILayout.Label("Waiting for host snapshots; inputs paused.");
                if (Finished()) GUILayout.Label(latest.Fighters[0].Hp == latest.Fighters[1].Hp ? "DRAW - host can start a new round" : "P" + (latest.Fighters[0].Hp > 0 ? 1 : 2) + " WINS - host can start a new round");
                if (PhotonNetwork.IsMasterClient)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("New live round")) StartRound(-1);
                    if (GUILayout.Button("Test dodge +10ms")) StartRound(10);
                    if (GUILayout.Button("Test dodge +100ms")) StartRound(100);
                    if (GUILayout.Button("Test dodge +110ms")) StartRound(110);
                    if (GUILayout.Button("Range test")) StartRound(-2);
                    if (GUILayout.Button("Replay / cancel test")) StartRound(-4);
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label("Test timings are input offsets, not guaranteed arrival times. No artificial 120 ms delay is added online.");
            }
            if (GUILayout.Button("Leave match / return to lobby")) Abort("You left the match.");
            GUILayout.EndArea(); GUI.matrix = previous;
        }
    }
}
