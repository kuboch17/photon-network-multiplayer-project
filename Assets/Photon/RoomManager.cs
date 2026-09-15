using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.UI;

public class RoomManager : MonoBehaviourPunCallbacks
{
    [SerializeField] private InputField roomNameInput;  // Attach the RoomNameInput field in the Inspector
    [SerializeField] private Button createRoomButton;   // Attach the Create Room button in the Inspector
    [SerializeField] private Button joinRoomButton;

    void Start()
    {
        ConnectToPhoton();

        joinRoomButton.onClick.AddListener(() =>
        {
            string roomName = roomNameInput.text;
            JoinSpecificRoom(roomName);
        });

        // Make sure the button is enabled only after connecting to Photon
        //createRoomButton.interactable = PhotonNetwork.IsConnectedAndReady;

        // Assign the button's onClick listener to create a room
        createRoomButton.onClick.AddListener(CreateRoomWithCustomName);
    }

    public void JoinSpecificRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName); // Join a room by name
    }

    // Callback if joining the room fails (e.g., if the room doesn’t exist)
    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Failed to join the room: " + message);
    }

    // Method to create a room with a specific name
    public void CreateRoomWithCustomName()
    {
        string roomName = roomNameInput.text;  // Get the room name from the input field

        if (!string.IsNullOrEmpty(roomName))
        {
            RoomOptions roomOptions = new RoomOptions { MaxPlayers = 4 };  // Set room options (e.g., max 4 players)
            PhotonNetwork.CreateRoom(roomName, roomOptions);  // Create the room with the given name
            Debug.Log("Room created with name: " + roomName);
        }
        else
        {
            Debug.LogError("Room name is empty. Please enter a valid name.");
        }
    }

    // Callback if room creation is successful
    public override void OnCreatedRoom()
    {
        Debug.Log("Successfully created room: " + PhotonNetwork.CurrentRoom.Name);
    }

    // Callback if room creation fails (e.g., room with the same name already exists)
    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Debug.LogError("Room creation failed: " + message);
    }

    // Connect to Photon servers
    public void ConnectToPhoton()
    {
        if (!PhotonNetwork.IsConnected)
        {
            // Set the game version to ensure that users are on the same version
            PhotonNetwork.GameVersion = "1.0";
            PhotonNetwork.ConnectUsingSettings(); // Connect to Photon server
        }
    }

    // Callback when connected to the Photon Master server
    public override void OnConnectedToMaster()
    {
        Debug.Log("Connected to Photon Master server!");

        // After connecting to the master, join a lobby
        PhotonNetwork.JoinLobby();
    }

    // Callback when connected to a lobby
    public override void OnJoinedLobby()
    {
        Debug.Log("Joined the lobby!");

        // You can now list available rooms or create a new one
    }

    // Callback if there's an error in connecting
    public override void OnDisconnected(DisconnectCause cause)
    {
        Debug.LogError("Disconnected from Photon: " + cause.ToString());
    }

    

}
