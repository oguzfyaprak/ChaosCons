using UnityEngine;
using TMPro;
using Steamworks;
using FishNet;
using FishNet.Transporting;
using FishNet.Managing;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance;

    [Header("UI")]
    public TMP_Text lobbyStatusText;
    public GameObject mainMenuUI;

    private Callback<LobbyCreated_t> lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> joinRequested;
    private Callback<LobbyEnter_t> lobbyEntered;

    private const string HOST_ADDRESS_KEY = "HostAddress";
    private CSteamID currentLobbyId;

    public TMP_InputField lobbyIDInputField;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (!SteamManager.Initialized)
        {
            lobbyStatusText.text = "Steam başlatılamadı.";
            return;
        }

        lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnJoinRequested);
        lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void CreateLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    public void JoinLobbyManually()
    {
        if (ulong.TryParse(lobbyIDInputField.text, out ulong lobbyId))
        {
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyId));
        }
        else
        {
            lobbyStatusText.text = "Geçersiz Lobby ID!";
        }
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            lobbyStatusText.text = "Lobi oluşturulamadı.";
            return;
        }

        currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(currentLobbyId, HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());

        lobbyStatusText.text = "Lobi oluşturuldu.";
        InstanceFinder.ServerManager.StartConnection();
    }

    private void OnJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        string hostAddress = SteamMatchmaking.GetLobbyData(new CSteamID(callback.m_ulSteamIDLobby), HOST_ADDRESS_KEY);
        lobbyStatusText.text = "Lobiye katıldınız. Host: " + hostAddress;

        InstanceFinder.ClientManager.StartConnection();
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
