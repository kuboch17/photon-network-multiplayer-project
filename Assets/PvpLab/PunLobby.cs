using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

namespace PvpLab
{
    public sealed class PunLobby : MonoBehaviourPunCallbacks
    {
        public const string LobbyScene = "menu", GameScene = "hunting";
        public static PunLobby Instance { get; private set; }
        private string nickname = "Player", roomName = "hunting-demo", status = "Enter your name, then connect.";
        private readonly string[] regions = { "eu", "us", "asia" };
        private int region;
        private bool busy, leaving;
        private float smokeRetryAt;
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this; DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
            nickname = PlayerPrefs.GetString("Duel.Nickname", "Player");
        }
        private void Start()
        {
            if (!PunSmoke.Enabled || Instance != this) return;
            nickname = "Smoke-" + PunSmoke.Role;
            roomName = PunSmoke.Room;
            Connect();
        }
        private void Update()
        {
            if (!PunSmoke.Enabled || PunSmoke.Done || Instance != this) return;
            PunSmoke.CheckDeadline();
            if (SceneManager.GetActiveScene().name != LobbyScene || busy || leaving) return;
            if (PhotonNetwork.InRoom)
            {
                if (PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2) StartMatch();
            }
            else if (PhotonNetwork.IsConnectedAndReady && Time.realtimeSinceStartup >= smokeRetryAt)
            {
                smokeRetryAt = Time.realtimeSinceStartup + 1;
                if (PunSmoke.Role == "host") CreateRoom(); else JoinRoom();
            }
        }
        private void Connect()
        {
            Guid id;
            if (PhotonNetwork.PhotonServerSettings == null ||
                !Guid.TryParse(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime, out id))
            { status = "Set a valid PUN App ID in PhotonServerSettings first (see the guide)."; return; }
            if (PhotonNetwork.IsConnected) return;
            PhotonNetwork.OfflineMode = false;
            PhotonNetwork.AutomaticallySyncScene = true;
            PhotonNetwork.GameVersion = PunCombatProtocol.Version;
            PhotonNetwork.NickName = string.IsNullOrWhiteSpace(nickname) ? "Player" : nickname.Trim();
            if (!PunSmoke.Enabled) PlayerPrefs.SetString("Duel.Nickname", PhotonNetwork.NickName);
            PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion = regions[region];
            PhotonNetwork.SendRate = 30;
            PhotonNetwork.SerializationRate = 20;
            busy = PhotonNetwork.ConnectUsingSettings();
            status = busy ? "Connecting to Photon..." : "Connection could not start. Try again.";
        }
        private bool ValidRoomName()
        {
            roomName = roomName.Trim();
            if (roomName.Length > 0 && roomName.Length <= 24) return true;
            status = "Enter a room name (1-24 characters)."; return false;
        }
        private void CreateRoom()
        {
            if (!ValidRoomName()) return;
            var options = new RoomOptions { MaxPlayers = 2, IsVisible = false, IsOpen = true,
                PlayerTtl = 0, EmptyRoomTtl = 0, CleanupCacheOnLeave = true,
                CustomRoomProperties = new Hashtable { { "duel.version", PunCombatProtocol.Version } } };
            busy = PhotonNetwork.CreateRoom(roomName, options, TypedLobby.Default);
            status = busy ? "Creating room..." : "Could not create room.";
        }
        private void JoinRoom()
        {
            if (!ValidRoomName()) return;
            busy = PhotonNetwork.JoinRoom(roomName);
            status = busy ? "Joining room..." : "Could not join room.";
        }
        private void StartMatch()
        {
            if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount != 2 || busy) return;
            int other = -1;
            foreach (Player player in PhotonNetwork.PlayerListOthers) other = player.ActorNumber;
            if (other < 0) return;
            var properties = new Hashtable {
                { PunCombatProtocol.MatchKey, Guid.NewGuid().ToString("N") },
                { PunCombatProtocol.PlayerOneKey, PhotonNetwork.LocalPlayer.ActorNumber },
                { PunCombatProtocol.PlayerTwoKey, other }
            };
            PhotonNetwork.CurrentRoom.IsOpen = false;
            busy = PhotonNetwork.CurrentRoom.SetCustomProperties(properties);
            status = "Preparing hunting...";
            // Wait for Photon to confirm properties before telling both peers to load.
        }
        public override void OnRoomPropertiesUpdate(Hashtable changed)
        {
            if (SceneManager.GetActiveScene().name != LobbyScene || !PhotonNetwork.IsMasterClient || leaving) return;
            if (changed.ContainsKey(PunCombatProtocol.MatchKey) && PhotonNetwork.CurrentRoom.PlayerCount == 2)
                PhotonNetwork.LoadLevel(GameScene);
        }
        public override void OnConnectedToMaster() { busy = false; status = "Connected. Create a room or join your friend's room."; }
        public override void OnCreatedRoom() { status = "Room created. Share its name with the other player."; }
        public override void OnJoinedRoom()
        {
            busy = false;
            if (PunSmoke.Enabled) Debug.Log("PUN-SMOKE " + PunSmoke.Role + " JOIN actor=" + PhotonNetwork.LocalPlayer.ActorNumber);
            object version;
            if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("duel.version", out version) ||
                !(version is string) || (string)version != PunCombatProtocol.Version || PhotonNetwork.CurrentRoom.MaxPlayers != 2)
            { ReturnToLobby("This is not a compatible two-player duel room."); return; }
            status = "Waiting for two players. The host starts the match.";
        }
        public override void OnPlayerEnteredRoom(Player player) { status = player.NickName + " joined. Ready to start."; }
        public override void OnPlayerLeftRoom(Player player)
        {
            if (SceneManager.GetActiveScene().name != LobbyScene) return;
            busy = false; status = "The other player left. Waiting for a player.";
            if (PhotonNetwork.IsMasterClient) PhotonNetwork.CurrentRoom.IsOpen = true;
        }
        public override void OnCreateRoomFailed(short code, string message) { busy = false; status = "Create failed: " + message; }
        public override void OnJoinRoomFailed(short code, string message) { busy = false; status = "Join failed: " + message; }
        public override void OnDisconnected(DisconnectCause cause)
        {
            if (PunSmoke.Enabled && !PunSmoke.Done) PunSmoke.Finish(false, "Disconnected: " + cause);
            busy = false; leaving = false;
            status = "Disconnected: " + cause + ". Check your connection/App ID and reconnect.";
            PhotonNetwork.AutomaticallySyncScene = false;
            if (SceneManager.GetActiveScene().name != LobbyScene) SceneManager.LoadScene(LobbyScene);
        }
        public void ReturnToLobby(string reason)
        {
            if (leaving) return;
            status = reason; busy = false;
            PhotonNetwork.AutomaticallySyncScene = false;
            if (PhotonNetwork.InRoom)
            {
                leaving = PhotonNetwork.LeaveRoom(false);
                if (!leaving) PhotonNetwork.Disconnect();
            }
            else if (SceneManager.GetActiveScene().name != LobbyScene) SceneManager.LoadScene(LobbyScene);
        }
        public override void OnLeftRoom()
        {
            leaving = false; busy = false;
            if (SceneManager.GetActiveScene().name != LobbyScene) SceneManager.LoadScene(LobbyScene);
        }
        private void OnGUI()
        {
            if (Instance != this || SceneManager.GetActiveScene().name != LobbyScene) return;
            float scale = Mathf.Min(Screen.width / 850f, Screen.height / 600f);
            Matrix4x4 previous = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            GUILayout.BeginArea(new Rect(125, 70, 600, 460), GUI.skin.box);
            GUILayout.Label("HUNTING - ONLINE PvP");
            GUILayout.Label("Two players / Photon PUN / host-authoritative combat");
            GUILayout.Space(15);
            GUILayout.Label(status);
            GUILayout.Label("Network: " + PhotonNetwork.NetworkClientState);
            if (!PhotonNetwork.IsConnected)
            {
                GUI.enabled = !busy;
                GUILayout.Label("Your name"); nickname = GUILayout.TextField(nickname, 20);
                GUILayout.Label("Region (both players must choose the same)"); region = GUILayout.Toolbar(region, regions);
                if (GUILayout.Button("Connect to Photon", GUILayout.Height(40))) Connect();
                GUI.enabled = true;
            }
            else if (!PhotonNetwork.InRoom)
            {
                GUILayout.Label("Region: " + PhotonNetwork.CloudRegion + " | Photon ping: " + PhotonNetwork.GetPing() + " ms");
                GUILayout.Label("Room name (share this with the second player)"); roomName = GUILayout.TextField(roomName, 24);
                GUI.enabled = !busy && PhotonNetwork.IsConnectedAndReady;
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Create room")) { PhotonNetwork.AutomaticallySyncScene = true; CreateRoom(); }
                if (GUILayout.Button("Join room")) { PhotonNetwork.AutomaticallySyncScene = true; JoinRoom(); }
                GUILayout.EndHorizontal(); GUI.enabled = true;
                if (GUILayout.Button("Disconnect / change region")) PhotonNetwork.Disconnect();
            }
            else
            {
                GUILayout.Label("Room: " + PhotonNetwork.CurrentRoom.Name + " | " + PhotonNetwork.CurrentRoom.PlayerCount + "/2");
                foreach (Player player in PhotonNetwork.PlayerList)
                    GUILayout.Label(player.NickName + (player.IsMasterClient ? " (host)" : "") + (player.IsLocal ? " (you)" : ""));
                GUI.enabled = !busy && !leaving && PhotonNetwork.IsMasterClient && PhotonNetwork.CurrentRoom.PlayerCount == 2;
                if (GUILayout.Button("Start hunting duel", GUILayout.Height(40))) StartMatch();
                GUI.enabled = !leaving;
                if (GUILayout.Button("Leave room")) ReturnToLobby("Left room.");
                GUI.enabled = true;
            }
            GUILayout.Space(15);
            GUILayout.Label("In game: A/D = move, J = dash attack, K = dodge.");
            GUILayout.Label("Use the same build and App ID on both computers.");
            GUILayout.EndArea(); GUI.matrix = previous;
        }
    }
}
