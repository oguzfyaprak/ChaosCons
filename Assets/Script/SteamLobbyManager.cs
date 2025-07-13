using UnityEngine;
using Steamworks;
using FishNet.Transporting;
using FishNet;
using FishNet.Managing;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance;

    private Callback<LobbyCreated_t> _lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequest;
    private Callback<LobbyEnter_t> _lobbyEntered;

    private const string HOST_ADDRESS_KEY = "HostAddress";

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyJoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequest);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    public void Quit()
    {
        Application.Quit();
        Debug.Log("Uygulama kapatılıyor...");
    }

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK) return;
        Debug.Log("Lobi oluşturuldu.");
        SteamMatchmaking.SetLobbyData((CSteamID)result.m_ulSteamIDLobby, HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());
        InstanceFinder.ServerManager.StartConnection();
    }

    private void OnLobbyJoinRequest(GameLobbyJoinRequested_t request)
    {
        SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if (InstanceFinder.NetworkManager.IsServerStarted)
            return;

        string hostAddress = SteamMatchmaking.GetLobbyData((CSteamID)result.m_ulSteamIDLobby, HOST_ADDRESS_KEY);
        InstanceFinder.TransportManager.Transport.SetClientAddress(hostAddress);
        InstanceFinder.ClientManager.StartConnection();
    }

}
