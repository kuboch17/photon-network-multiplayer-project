using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

public class LobbyManager : MonoBehaviourPunCallbacks
{
    // Start is called before the first frame update
    void Start()
    {
        ConnectToPhoton();
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
