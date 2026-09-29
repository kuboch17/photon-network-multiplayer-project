using System.Collections;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PvpLab
{
    // Keeps the existing Character controller for the local player.  PUN carries
    // proposed movement to the Master Client, which validates and republishes it.
    public sealed class HuntingCombatDemo : MonoBehaviourPunCallbacks, IOnEventCallback
    {
        [SerializeField] private Transform playerRoot = null;

        private const byte MovementProposalEvent = 80;
        private const byte AuthoritativeStateEvent = 81;
        private const byte CombatInputEvent = 82;
        private const byte CombatVisualEvent = 83;
        private const float SendInterval = .05f;
        private const float MaximumSpeed = 15f;
        private const float AttackRange = 2.2f;
        private const int AttackDamage = 25;
        private const int AttackWindupMs = 100;
        private const int ResolveGraceMs = 30;
        private const int DodgeWindowMs = 120;
        private const int ActionCooldownMs = 600;

        private static HuntingCombatDemo current;
        private readonly Transform[] characters = new Transform[2];
        private readonly Animator[] animators = new Animator[2];
        private readonly Vector3[] targetPosition = new Vector3[2];
        private readonly Quaternion[] targetRotation = new Quaternion[2];
        private readonly Vector3[] authorityPosition = new Vector3[2];
        private readonly int[] moveSequence = new int[2];
        private readonly int[] lastMoveSequence = new int[2];
        private readonly int[] lastMoveAt = new int[2];
        private readonly int[] actionSequence = new int[2];
        private readonly int[] lastActionSequence = new int[2];
        private readonly int[] readyAt = new int[2];
        private readonly int[] dodgeFrom = new int[2];
        private readonly int[] dodgeUntil = new int[2];
        private readonly int[] pendingContact = new int[2];
        private readonly int[] pendingResolve = new int[2];
        private readonly int[] hp = { 100, 100 };

        private int localSlot = -1;
        private string matchId;
        private float nextSend;
        private Vector3 lastSentPosition;
        private bool prepared;
        private bool leaving;
        private string combatResult = "Move close to the opponent.";
        private int rejectedInputs;
        private bool receivedNetworkState;
        private float smokeStarted;

        public bool IsPrepared { get { return prepared; } }
        public string PreparationError { get; private set; }

        public static bool Owns(Transform target)
        {
            if (current == null || target == null) return false;
            for (int i = 0; i < current.characters.Length; i++)
                if (current.characters[i] != null && target.IsChildOf(current.characters[i])) return true;
            return false;
        }

        private IEnumerator Start()
        {
            current = this;
            Application.runInBackground = true;
            if (!PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom.PlayerCount != 2)
            {
                PreparationError = "This scene must be opened by two players from the lobby.";
                yield break;
            }
            yield return PrepareCharacterPair();
            if (!prepared) yield break;
            smokeStarted = Time.realtimeSinceStartup;
        }

        private IEnumerator PrepareCharacterPair()
        {
            object p1, p2, id;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PunCombatProtocol.PlayerOneKey, out p1) ||
                !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PunCombatProtocol.PlayerTwoKey, out p2) ||
                !PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(PunCombatProtocol.MatchKey, out id))
            {
                PreparationError = "The room has no player assignment.";
                yield break;
            }
            int actor = PhotonNetwork.LocalPlayer.ActorNumber;
            localSlot = actor == (int)p1 ? 0 : actor == (int)p2 ? 1 : -1;
            matchId = id as string;
            if (localSlot < 0 || string.IsNullOrEmpty(matchId) || playerRoot == null)
            {
                PreparationError = "Invalid player assignment or Character1 reference.";
                yield break;
            }

            GameObject original = playerRoot.gameObject;
            original.name = "Character1";
            Vector3 spawn1 = original.transform.position;
            Quaternion rotation1 = original.transform.rotation;
            original.SetActive(false);
            GameObject copy = Instantiate(original, spawn1 + original.transform.right * 3f, Quaternion.Euler(0, rotation1.eulerAngles.y + 180f, 0));
            copy.name = "Character2";
            // Character1's Awake added these helpers at runtime.  The clone will
            // add its own pair when activated, so do not keep copied instances.
            foreach (MonoBehaviour helper in copy.GetComponentsInChildren<MonoBehaviour>(true))
                if (helper.GetType().Name == "RPGCharacterAnimatorEvents" || helper.GetType().Name == "AnimatorParentMove")
                    DestroyImmediate(helper);
            characters[0] = original.transform;
            characters[1] = copy.transform;

            ConfigureCharacter(characters[0], localSlot == 0);
            ConfigureCharacter(characters[1], localSlot == 1);
            original.SetActive(true);
            copy.SetActive(true);
            yield return null;

            for (int i = 0; i < 2; i++)
            {
                animators[i] = characters[i].GetComponentInChildren<Animator>();
                targetPosition[i] = authorityPosition[i] = characters[i].position;
                targetRotation[i] = characters[i].rotation;
                lastMoveAt[i] = PhotonNetwork.ServerTimestamp;
            }
            lastSentPosition = characters[localSlot].position;
            RetargetCamera(characters[localSlot]);
            prepared = true;
            combatResult = "Character" + (localSlot + 1) + " is yours. Character" + (2 - localSlot) + " is remote.";
            Debug.Log("PUN CHARACTER READY local=Character" + (localSlot + 1));
        }

        private static void ConfigureCharacter(Transform root, bool locallyOwned)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                string type = behaviour.GetType().Name;
                if (type == "RPGCharacterInputController")
                {
                    SetBoolField(behaviour, "playa1", locallyOwned);
                    SetBoolField(behaviour, "playa2", false);
                    SetBoolField(behaviour, "allowedInput", locallyOwned);
                }
                else if (type == "RPGCharacterController")
                {
                    SetBoolField(behaviour, "playa1", locallyOwned);
                    SetBoolField(behaviour, "playa2", false);
                }
            }
            if (locallyOwned) return;

            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }
        }

        private static void RetargetCamera(Transform target)
        {
            foreach (MonoBehaviour camera in FindObjectsOfType<MonoBehaviour>())
            {
                System.Type type = camera.GetType();
                System.Reflection.PropertyInfo follow = type.GetProperty("Follow");
                System.Reflection.PropertyInfo lookAt = type.GetProperty("LookAt");
                if (follow != null && follow.CanWrite && follow.PropertyType == typeof(Transform)) follow.SetValue(camera, target, null);
                if (lookAt != null && lookAt.CanWrite && lookAt.PropertyType == typeof(Transform)) lookAt.SetValue(camera, target, null);
            }
        }

        private static void SetBoolField(MonoBehaviour target, string name, bool value)
        {
            System.Reflection.FieldInfo field = target.GetType().GetField(name);
            if (field != null && field.FieldType == typeof(bool)) field.SetValue(target, value);
        }

        private void Update()
        {
            if (!prepared || leaving) return;
            int now = PhotonNetwork.ServerTimestamp;
            if (PhotonNetwork.IsMasterClient) ResolvePendingAttacks(now);

            if (Time.unscaledTime >= nextSend)
            {
                nextSend = Time.unscaledTime + SendInterval;
                SendMovement(now);
            }

            bool attackPressed = Input.GetKeyDown(KeyCode.J) || Input.GetButtonDown("AttackL") || Input.GetButtonDown("AttackR");
            if (attackPressed) SubmitCombat(0);
            if (Input.GetKeyDown(KeyCode.K)) SubmitCombat(1);

            if (PunSmoke.Enabled && !PunSmoke.Done && receivedNetworkState && Time.realtimeSinceStartup - smokeStarted > 2f)
                PunSmoke.Finish(characters[0] != null && characters[1] != null && localSlot >= 0,
                    "pair synced local=Character" + (localSlot + 1));
        }

        private void LateUpdate()
        {
            if (!prepared) return;
            int remote = 1 - localSlot;
            characters[remote].position = Vector3.Lerp(characters[remote].position, targetPosition[remote], 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
            characters[remote].rotation = Quaternion.Slerp(characters[remote].rotation, targetRotation[remote], 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        }

        private void SendMovement(int now)
        {
            Transform local = characters[localSlot];
            Vector3 velocity = (local.position - lastSentPosition) / Mathf.Max(SendInterval, .001f);
            lastSentPosition = local.position;
            Animator animator = animators[localSlot];
            object[] packet =
            {
                matchId, localSlot, ++moveSequence[localSlot], now,
                local.position.x, local.position.y, local.position.z,
                local.rotation.x, local.rotation.y, local.rotation.z, local.rotation.w,
                velocity.x, velocity.y, velocity.z,
                animator != null && animator.GetBool("Moving"),
                animator != null ? animator.GetFloat("Velocity Z") : 0f,
                animator != null && animator.GetBool("Sprint"),
                animator != null ? animator.GetInteger("Jumping") : 0
            };
            if (PhotonNetwork.IsMasterClient) AcceptMovement(PhotonNetwork.LocalPlayer.ActorNumber, packet);
            else PhotonNetwork.RaiseEvent(MovementProposalEvent, packet,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendUnreliable);
        }

        private void AcceptMovement(int sender, object customData)
        {
            object[] d = customData as object[];
            if (d == null || d.Length != 18 || (string)d[0] != matchId) return;
            int slot = (int)d[1], seq = (int)d[2], now = PhotonNetwork.ServerTimestamp;
            if (slot != SlotForActor(sender) || seq <= lastMoveSequence[slot]) { rejectedInputs++; return; }
            Vector3 proposed = new Vector3((float)d[4], (float)d[5], (float)d[6]);
            float seconds = Mathf.Clamp(PunCombatProtocol.Elapsed(now, lastMoveAt[slot]) / 1000f, .01f, .25f);
            float allowed = MaximumSpeed * seconds + .75f;
            Vector3 from = authorityPosition[slot];
            Vector3 delta = proposed - from;
            if (lastMoveSequence[slot] > 0 && delta.magnitude > allowed)
            {
                proposed = from + delta.normalized * allowed;
                rejectedInputs++;
            }
            authorityPosition[slot] = proposed;
            targetPosition[slot] = proposed;
            targetRotation[slot] = new Quaternion((float)d[7], (float)d[8], (float)d[9], (float)d[10]);
            lastMoveSequence[slot] = seq;
            lastMoveAt[slot] = now;
            d[4] = proposed.x; d[5] = proposed.y; d[6] = proposed.z; d[3] = now;
            if (slot != localSlot) ApplyAnimation(slot, (bool)d[14], (float)d[15], (bool)d[16], (int)d[17]);
            if (slot != localSlot) receivedNetworkState = true;
            BroadcastState(d);
        }

        private void BroadcastState(object[] movement)
        {
            object[] state = new object[movement.Length + 4];
            movement.CopyTo(state, 0);
            state[18] = hp[0];
            state[19] = hp[1];
            state[20] = rejectedInputs;
            state[21] = combatResult;
            PhotonNetwork.RaiseEvent(AuthoritativeStateEvent, state,
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendUnreliable);
        }

        private void ReceiveState(object customData)
        {
            object[] d = customData as object[];
            if (d == null || d.Length != 22 || (string)d[0] != matchId) return;
            int slot = (int)d[1];
            if (slot < 0 || slot > 1 || (int)d[2] < lastMoveSequence[slot]) return;
            lastMoveSequence[slot] = (int)d[2];
            Vector3 position = new Vector3((float)d[4], (float)d[5], (float)d[6]);
            Quaternion rotation = new Quaternion((float)d[7], (float)d[8], (float)d[9], (float)d[10]);
            targetPosition[slot] = position;
            targetRotation[slot] = rotation;
            authorityPosition[slot] = position;
            hp[0] = (int)d[18]; hp[1] = (int)d[19]; rejectedInputs = (int)d[20]; combatResult = (string)d[21];
            receivedNetworkState = true;
            if (slot == localSlot && Vector3.Distance(characters[slot].position, position) > 1.5f)
                characters[slot].position = position;
            ApplyAnimation(slot, (bool)d[14], (float)d[15], (bool)d[16], (int)d[17]);
        }

        private void ApplyAnimation(int slot, bool moving, float speed, bool sprint, int jumping)
        {
            Animator animator = animators[slot];
            if (animator == null || slot == localSlot) return;
            animator.SetBool("Moving", moving);
            animator.SetFloat("Velocity Z", speed);
            animator.SetBool("Sprint", sprint);
            animator.SetInteger("Jumping", jumping);
        }

        private void SubmitCombat(byte kind)
        {
            int now = PhotonNetwork.ServerTimestamp;
            int seq = ++actionSequence[localSlot];
            PlayCombatVisual(localSlot, kind, true);
            object[] packet = { matchId, localSlot, seq, kind };
            if (PhotonNetwork.IsMasterClient) AcceptCombat(PhotonNetwork.LocalPlayer.ActorNumber, packet, now);
            else PhotonNetwork.RaiseEvent(CombatInputEvent, packet,
                new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
        }

        private void AcceptCombat(int sender, object customData, int now)
        {
            object[] d = customData as object[];
            if (d == null || d.Length != 4 || (string)d[0] != matchId) return;
            int slot = (int)d[1], seq = (int)d[2];
            byte kind = (byte)d[3];
            if (slot != SlotForActor(sender) || seq <= lastActionSequence[slot] || kind > 1 || now < readyAt[slot] || hp[slot] <= 0)
            { rejectedInputs++; return; }
            lastActionSequence[slot] = seq;
            readyAt[slot] = now + ActionCooldownMs;
            if (kind == 1)
            {
                // A small fixed backdate is the entire dodge leniency budget.
                dodgeFrom[slot] = now - ResolveGraceMs;
                dodgeUntil[slot] = now + DodgeWindowMs;
                combatResult = "P" + (slot + 1) + " dodge accepted by host.";
            }
            else
            {
                pendingContact[slot] = now + AttackWindupMs;
                pendingResolve[slot] = pendingContact[slot] + ResolveGraceMs;
                combatResult = "P" + (slot + 1) + " attack queued; host decides at contact time.";
            }
            if (slot != localSlot) PlayCombatVisual(slot, kind, false);
            PhotonNetwork.RaiseEvent(CombatVisualEvent, new object[] { matchId, slot, seq, kind },
                new RaiseEventOptions { Receivers = ReceiverGroup.Others }, SendOptions.SendReliable);
        }

        private void ResolvePendingAttacks(int now)
        {
            for (int attacker = 0; attacker < 2; attacker++)
            {
                if (pendingResolve[attacker] == 0 || PunCombatProtocol.Elapsed(now, pendingResolve[attacker]) < 0) continue;
                int defender = 1 - attacker;
                int contact = pendingContact[attacker];
                bool dodged = dodgeFrom[defender] <= contact && dodgeUntil[defender] >= contact;
                float distance = Vector3.Distance(authorityPosition[attacker], authorityPosition[defender]);
                if (dodged) combatResult = "DODGE: P" + (defender + 1) + " was invulnerable at contact time.";
                else if (distance <= AttackRange)
                {
                    hp[defender] = Mathf.Max(0, hp[defender] - AttackDamage);
                    combatResult = "HIT: host confirmed range and dealt " + AttackDamage + " damage.";
                }
                else combatResult = "MISS: host measured " + distance.ToString("F1") + " m (range " + AttackRange.ToString("F1") + " m).";
                pendingContact[attacker] = pendingResolve[attacker] = 0;
                BroadcastCurrentStates();
            }
        }

        private void BroadcastCurrentStates()
        {
            for (int slot = 0; slot < 2; slot++)
            {
                Animator a = animators[slot];
                object[] movement = { matchId, slot, lastMoveSequence[slot], PhotonNetwork.ServerTimestamp,
                    authorityPosition[slot].x, authorityPosition[slot].y, authorityPosition[slot].z,
                    targetRotation[slot].x, targetRotation[slot].y, targetRotation[slot].z, targetRotation[slot].w,
                    0f, 0f, 0f, a != null && a.GetBool("Moving"), a != null ? a.GetFloat("Velocity Z") : 0f,
                    a != null && a.GetBool("Sprint"), a != null ? a.GetInteger("Jumping") : 0 };
                BroadcastState(movement);
            }
        }

        private void PlayCombatVisual(int slot, byte kind, bool localPrediction)
        {
            Animator animator = animators[slot];
            if (animator == null) return;
            animator.SetInteger("Weapon", 0);
            animator.SetInteger("Action", 1);
            animator.SetTrigger(kind == 1 ? "DodgeTrigger" : "AttackTrigger");
            if (kind == 0 && localPrediction && Input.GetKeyDown(KeyCode.J))
                characters[slot].position += characters[slot].forward * 1f;
        }

        private int SlotForActor(int actor)
        {
            object p1 = PhotonNetwork.CurrentRoom.CustomProperties[PunCombatProtocol.PlayerOneKey];
            object p2 = PhotonNetwork.CurrentRoom.CustomProperties[PunCombatProtocol.PlayerTwoKey];
            return actor == (int)p1 ? 0 : actor == (int)p2 ? 1 : -1;
        }

        public void OnEvent(EventData photonEvent)
        {
            if (!prepared) return;
            if (photonEvent.Code == MovementProposalEvent && PhotonNetwork.IsMasterClient) AcceptMovement(photonEvent.Sender, photonEvent.CustomData);
            else if (photonEvent.Code == AuthoritativeStateEvent && !PhotonNetwork.IsMasterClient) ReceiveState(photonEvent.CustomData);
            else if (photonEvent.Code == CombatInputEvent && PhotonNetwork.IsMasterClient) AcceptCombat(photonEvent.Sender, photonEvent.CustomData, PhotonNetwork.ServerTimestamp);
            else if (photonEvent.Code == CombatVisualEvent)
            {
                object[] d = photonEvent.CustomData as object[];
                if (d != null && d.Length == 4 && (string)d[0] == matchId && (int)d[1] != localSlot) PlayCombatVisual((int)d[1], (byte)d[3], false);
            }
        }

        public override void OnPlayerLeftRoom(Player otherPlayer) { Leave("The other player disconnected."); }
        public override void OnMasterClientSwitched(Player newMasterClient) { Leave("The host disconnected; authority migration is disabled for this demo."); }

        private void Leave(string reason)
        {
            if (leaving) return;
            leaving = true;
            Debug.LogWarning(reason);
            PhotonNetwork.AutomaticallySyncScene = false;
            if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom(false); else SceneManager.LoadScene("menu");
        }

        public override void OnLeftRoom() { SceneManager.LoadScene("menu"); }

        private void OnGUI()
        {
            if (!prepared) return;
            GUILayout.BeginArea(new Rect(16, 16, 480, 160), GUI.skin.box);
            GUILayout.Label("YOU CONTROL: Character" + (localSlot + 1) + "   |   Photon ping: " + PhotonNetwork.GetPing() + " ms");
            GUILayout.Label("Character1 HP: " + hp[0] + "        Character2 HP: " + hp[1]);
            GUILayout.Label(combatResult);
            GUILayout.Label("Existing movement controls  |  J: dash attack  |  K: dodge");
            GUILayout.Label("Host authority rejected inputs: " + rejectedInputs);
            if (GUILayout.Button("Leave match")) Leave("You left the match.");
            GUILayout.EndArea();
        }

        // Kept for the old isolated simulation component so existing project code still compiles.
        public IEnumerator PrepareOnline() { yield break; }
        public void ResetOnlineVisuals() { }
        public void RenderOnline(CombatSimulation.Fighter[] fighters, int time, float delta) { }

        private void OnDestroy() { if (current == this) current = null; }
    }
}
