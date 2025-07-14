using UnityEngine;
using Steamworks;
using FishNet.Managing;
using FishNet.Managing.Scened;
using System.Collections.Generic;
using System.Linq;

public class SteamLobbyManager : MonoBehaviour
{
    public static SteamLobbyManager Instance;

    [SerializeField] private NetworkManager networkManager;

    private const string LOBBY_NAME_KEY = "name";
    private const string HOST_ADDRESS_KEY = "HostAddress";

    private Callback<LobbyCreated_t> _lobbyCreated;
    private Callback<GameLobbyJoinRequested_t> _lobbyJoinRequest;
    private Callback<LobbyEnter_t> _lobbyEntered;

    private CSteamID _currentLobbyId = CSteamID.Nil;

    private List<string> playerNames = new();
    private Dictionary<string, bool> readyStates = new();
    public TMPro.TextMeshProUGUI lobbyListText;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        _lobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        _lobbyJoinRequest = Callback<GameLobbyJoinRequested_t>.Create(OnLobbyJoinRequested);
        _lobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
    }

    public void HostLobby()
    {
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    }

    private void OnLobbyCreated(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("Lobi oluşturulamadı.");
            return;
        }

        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        SteamMatchmaking.SetLobbyData(_currentLobbyId, LOBBY_NAME_KEY, SteamFriends.GetPersonaName());
        SteamMatchmaking.SetLobbyData(_currentLobbyId, HOST_ADDRESS_KEY, SteamUser.GetSteamID().ToString());

        StartServer();
    }

    private void OnLobbyJoinRequested(GameLobbyJoinRequested_t callback)
    {
        SteamMatchmaking.JoinLobby(callback.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t callback)
    {
        if (networkManager.ServerManager.Started) return;

        Debug.Log("Lobiye katıldı: " + callback.m_ulSteamIDLobby);
        _currentLobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        StartClient();

        string newPlayerName = SteamFriends.GetPersonaName();
        if (!playerNames.Contains(newPlayerName))
        {
            playerNames.Add(newPlayerName);
            readyStates[newPlayerName] = false;
        }

        UpdateLobbyUI();
    }

    private void UpdateLobbyUI()
    {
        if (lobbyListText == null)
        {
            Debug.LogError("lobbyListText bağlı değil!");
            return;
        }

        string result = "";

        foreach (var name in playerNames)
        {
            if (!readyStates.ContainsKey(name))
                readyStates[name] = false;

            string ready = readyStates[name] ? "✅" : "❌";
            result += $"{name} - {ready}\n";
        }

        lobbyListText.text = result;

        // 🔐 Lobby ID henüz oluşmadıysa host kontrolüne girme
        if (_currentLobbyId == CSteamID.Nil) return;

        // ✅ Host mu? ve herkes hazır mı?
        if (SteamMatchmaking.GetLobbyOwner(_currentLobbyId) == SteamUser.GetSteamID())
        {
            bool everyoneReady = readyStates.Count > 0 && readyStates.Values.All(v => v);
            GameObject startBtn = GameObject.Find("StartButton");
            if (startBtn != null)
                startBtn.SetActive(everyoneReady);
        }
    }

    private void StartServer()
    {
        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();
    }

    private void StartClient()
    {
        networkManager.ClientManager.StartConnection();
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void StartGame()
    {
        Debug.Log("Oyunu başlat butonuna basıldı");
    }

    public void ToggleReady()
    {
        string name = SteamFriends.GetPersonaName();

        if (!playerNames.Contains(name))
            playerNames.Add(name);

        if (!readyStates.ContainsKey(name))
            readyStates[name] = false;

        readyStates[name] = !readyStates[name];
        UpdateLobbyUI();
    }
}
