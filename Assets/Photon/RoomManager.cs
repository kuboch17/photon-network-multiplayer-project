using System;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Reuses the UI in menu.unity for a focused two-player Photon flow.
public sealed class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private InputField roomNameInput;
    [SerializeField] private Button createRoomButton;
    [SerializeField] private Button joinRoomButton;

    private const string GameScene = "hunting";
    private Text statusText;
    private bool busy;
    private bool starting;
    private float smokeRetryAt;

    private void Awake()
    {
        Application.runInBackground = true;
        BuildSimpleLobbyLayout();
        createRoomButton.onClick.RemoveAllListeners();
        joinRoomButton.onClick.RemoveAllListeners();
        createRoomButton.onClick.AddListener(CreateRoom);
        joinRoomButton.onClick.AddListener(JoinRoom);
        roomNameInput.onEndEdit.AddListener(value => { if (Input.GetKeyDown(KeyCode.Return)) JoinRoom(); });
    }

    private void Start()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
        PhotonNetwork.GameVersion = PvpLab.PunCombatProtocol.Version;
        PhotonNetwork.SendRate = 30;
        PhotonNetwork.SerializationRate = 20;
        PhotonNetwork.NickName = PvpLab.PunSmoke.Enabled
            ? "Smoke-" + PvpLab.PunSmoke.Role
            : PlayerPrefs.GetString("Hunting.Nickname", "Player-" + UnityEngine.Random.Range(100, 999));
        if (PvpLab.PunSmoke.Enabled) roomNameInput.text = PvpLab.PunSmoke.Room;
        SetStatus("Connecting to Photon...");
        SetButtons(false);
        if (!PhotonNetwork.IsConnected) PhotonNetwork.ConnectUsingSettings();
        else OnConnectedToMaster();
    }

    private void Update()
    {
        if (!PvpLab.PunSmoke.Enabled || PvpLab.PunSmoke.Done) return;
        PvpLab.PunSmoke.CheckDeadline();
        if (PhotonNetwork.InRoom || !PhotonNetwork.IsConnectedAndReady || busy || Time.realtimeSinceStartup < smokeRetryAt) return;
        smokeRetryAt = Time.realtimeSinceStartup + 1f;
        if (PvpLab.PunSmoke.Role == "host") CreateRoom(); else JoinRoom();
    }

    private string RoomName()
    {
        string value = roomNameInput.text.Trim();
        if (value.Length > 0 && value.Length <= 24) return value;
        SetStatus("Enter a room code with 1-24 characters.");
        return null;
    }

    private void CreateRoom()
    {
        string room = RoomName();
        if (room == null || busy || !PhotonNetwork.IsConnectedAndReady) return;
        busy = true;
        SetButtons(false);
        SetStatus("Creating room " + room + "...");
        var options = new RoomOptions
        {
            MaxPlayers = 2,
            IsOpen = true,
            IsVisible = false,
            CleanupCacheOnLeave = true,
            CustomRoomProperties = new Hashtable { { "duel.version", PvpLab.PunCombatProtocol.Version } }
        };
        PhotonNetwork.CreateRoom(room, options, TypedLobby.Default);
    }

    private void JoinRoom()
    {
        string room = RoomName();
        if (room == null || busy || !PhotonNetwork.IsConnectedAndReady) return;
        busy = true;
        SetButtons(false);
        SetStatus("Joining room " + room + "...");
        PhotonNetwork.JoinRoom(room);
    }

    private void PrepareMatch()
    {
        if (starting || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom.PlayerCount != 2) return;
        Player second = PhotonNetwork.PlayerListOthers[0];
        starting = true;
        PhotonNetwork.CurrentRoom.IsOpen = false;
        SetStatus("Player 2 connected. Starting hunting...");
        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
        {
            { PvpLab.PunCombatProtocol.MatchKey, Guid.NewGuid().ToString("N") },
            { PvpLab.PunCombatProtocol.PlayerOneKey, PhotonNetwork.LocalPlayer.ActorNumber },
            { PvpLab.PunCombatProtocol.PlayerTwoKey, second.ActorNumber }
        });
    }

    public override void OnConnectedToMaster()
    {
        busy = false;
        SetButtons(true);
        SetStatus("Connected. Create a room, then share its code with Player 2.");
    }

    public override void OnJoinedRoom()
    {
        busy = false;
        object version;
        if (!PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue("duel.version", out version) ||
            !(version is string) || (string)version != PvpLab.PunCombatProtocol.Version || PhotonNetwork.CurrentRoom.MaxPlayers != 2)
        {
            SetStatus("This room uses a different version.");
            PhotonNetwork.LeaveRoom();
            return;
        }
        SetButtons(false);
        SetStatus(PhotonNetwork.IsMasterClient ? "Room ready. Waiting for Player 2..." : "Connected as Player 2. Starting hunting...");
        PrepareMatch();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer) { PrepareMatch(); }

    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (PhotonNetwork.IsMasterClient && changed.ContainsKey(PvpLab.PunCombatProtocol.MatchKey)) PhotonNetwork.LoadLevel(GameScene);
    }

    public override void OnCreateRoomFailed(short code, string message) { Failed("Could not create room: " + message); }
    public override void OnJoinRoomFailed(short code, string message) { Failed("Could not join room: " + message); }
    public override void OnDisconnected(DisconnectCause cause)
    {
        busy = false;
        SetButtons(false);
        SetStatus("Disconnected: " + cause + ". Restart the game to reconnect.");
        if (PvpLab.PunSmoke.Enabled) PvpLab.PunSmoke.Finish(false, "Disconnected: " + cause);
    }

    private void Failed(string message)
    {
        busy = false;
        SetButtons(PhotonNetwork.IsConnectedAndReady);
        SetStatus(message);
    }

    private void SetButtons(bool enabled)
    {
        if (createRoomButton != null) createRoomButton.interactable = enabled;
        if (joinRoomButton != null) joinRoomButton.interactable = enabled;
        if (roomNameInput != null) roomNameInput.interactable = enabled;
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.Log("LOBBY: " + message);
    }

    private void BuildSimpleLobbyLayout()
    {
        RectTransform card = transform as RectTransform;
        card.anchorMin = card.anchorMax = new Vector2(.5f, .5f);
        card.anchoredPosition = Vector2.zero;
        card.sizeDelta = new Vector2(560, 390);
        Image cardImage = gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        cardImage.color = new Color(.075f, .105f, .13f, .97f);

        roomNameInput.transform.SetParent(card, false);
        createRoomButton.transform.SetParent(card, false);
        joinRoomButton.transform.SetParent(card, false);
        Layout(roomNameInput.GetComponent<RectTransform>(), 0, 45, 460, 48);
        Layout(createRoomButton.GetComponent<RectTransform>(), -118, -25, 224, 48);
        Layout(joinRoomButton.GetComponent<RectTransform>(), 118, -25, 224, 48);
        roomNameInput.placeholder.GetComponent<Text>().text = "Room code";
        roomNameInput.textComponent.fontSize = 21;
        StyleButton(createRoomButton, new Color(.16f, .64f, .49f), "CREATE ROOM");
        StyleButton(joinRoomButton, new Color(.18f, .45f, .78f), "JOIN ROOM");

        foreach (Text label in FindObjectsOfType<Text>()) if (label.text == "Loading...") statusText = label;
        if (statusText != null)
        {
            statusText.transform.SetParent(card, false);
            statusText.color = new Color(.72f, .78f, .82f);
            statusText.fontSize = 17;
            statusText.alignment = TextAnchor.MiddleCenter;
            Layout(statusText.rectTransform, 0, -100, 470, 65);
        }
        else statusText = CreateLabel(card, "Status", 0, -100, 470, 65, 17, new Color(.72f, .78f, .82f));

        CreateLabel(card, "HUNTING ONLINE", 0, 145, 480, 50, 32, Color.white).fontStyle = FontStyle.Bold;
        CreateLabel(card, "Two players  •  Photon PUN  •  server authority", 0, 105, 500, 30, 15, new Color(.45f, .72f, .67f));
        CreateLabel(card, "Both players enter the same room code", 0, 78, 480, 25, 15, new Color(.62f, .67f, .71f));
        CreateLabel(card, "The match starts automatically when Player 2 joins", 0, -153, 480, 28, 14, new Color(.45f, .5f, .54f));
    }

    private static void StyleButton(Button button, Color color, string caption)
    {
        button.GetComponent<Image>().color = color;
        Text text = button.GetComponentInChildren<Text>();
        text.text = caption;
        text.color = Color.white;
        text.fontSize = 17;
        text.fontStyle = FontStyle.Bold;
    }

    private static Text CreateLabel(Transform parent, string text, float x, float y, float w, float h, int size, Color color)
    {
        GameObject go = new GameObject(text, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text label = go.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAnchor.MiddleCenter;
        Layout(label.rectTransform, x, y, w, h);
        return label;
    }

    private static void Layout(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(w, h);
    }
}
