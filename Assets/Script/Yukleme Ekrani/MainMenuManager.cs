using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using FishNet.Managing.Scened;
using UnityEngine.UI;
using FishNet.Connection;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    public NetworkManager networkManager;
    [SerializeField] private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks steamworksTransportInstance;

    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbySelectionPanel;
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject settingsPanel;

    [SerializeField] private TMP_InputField lobbyIdInputField;
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject readyButton;

    [SerializeField] private string mainGameSceneName = "MainMap";
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> LobbyEntered;
    protected Callback<LobbyChatUpdate_t> LobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> LobbyDataUpdate;

    private CSteamID _currentLobbyID;
    public static CSteamID staticLobbyID;
    private LobbyManager _lobbyManagerInstance;

    void Awake()
    {
        Debug.Log("MainMenuManager Awake çalıştı.");

        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("NetworkManager bulunamadı!");
                return;
            }
        }

        steamworksTransportInstance = networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (steamworksTransportInstance == null)
        {
            Debug.LogError("FishySteamworks Transport bulunamadı!");
            return;
        }

        ShowPanel(mainMenuPanel);

        if (SteamManager.Initialized)
        {
            LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            LobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

            Debug.Log($"Steamworks.NET başlatıldı. Oyuncu Adı: {SteamFriends.GetPersonaName()}");
        }
        else
        {
            Debug.LogError("SteamAPI başlatılamadı!");
        }

        networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
        networkManager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionStateChanged;

        _lobbyManagerInstance = GetComponent<LobbyManager>();
        if (_lobbyManagerInstance == null)
        {
            Debug.LogError("LobbyManager bulunamadı!");
        }
        else
        {
            _lobbyManagerInstance.Initialize(networkManager, steamworksTransportInstance,
                                             lobbyIdText, playerListText, startGameButton,
                                             leaveLobbyButton, readyButton, mainGameSceneName);
        }
    }

    private void ShowPanel(GameObject panelToShow)
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbySelectionPanel != null) lobbySelectionPanel.SetActive(false);
        if (joinLobbyPanel != null) joinLobbyPanel.SetActive(false);
        if (lobbyPanel != null) lobbyPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        if (panelToShow != null)
            panelToShow.SetActive(true);
        else
            Debug.LogWarning("Gösterilecek panel null geldi!");
    }

    public void OnClick_PlayGame() => ShowPanel(lobbySelectionPanel);
    public void OnClick_Ayarlar() => ShowPanel(settingsPanel);
    public void OnClick_OyundanCik() => Application.Quit();
    public void OnClick_CreateLobby() => SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
    public void OnClick_JoinLobbyUI() => ShowPanel(joinLobbyPanel);
    public void OnClick_JoinLobbyById()
    {
        if (ulong.TryParse(lobbyIdInputField.text, out ulong lobbyID))
        {
            SteamMatchmaking.JoinLobby(new CSteamID(lobbyID));
        }
        else
        {
            Debug.LogError("Geçersiz Lobi ID formatı.");
        }
    }
    public void OnClick_BackFromLobbySelection() => ShowPanel(mainMenuPanel);
    public void OnClick_BackFromJoinLobby() => ShowPanel(lobbySelectionPanel);
    public void OnClick_BackFromSettings() => ShowPanel(mainMenuPanel);
    public void OnClick_LeaveCurrentLobby()
    {
        _lobbyManagerInstance.OnClick_LeaveLobby();
        ShowPanel(mainMenuPanel);
    }

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Lobi oluşturma hatası: {result.m_eResult}");
            return;
        }

        _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);

        // HOST → SUNUCUYU BAŞLAT!
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();

        // --- EN KRİTİK ADIM ---
        // Hostun portunu Steam'e bildir!

        ushort serverPort = 7777;
        SteamMatchmaking.SetLobbyGameServer(
        _currentLobbyID,
        0,
        serverPort, // Artık elle atadığınız portu kullanıyoruz.
        SteamUser.GetSteamID()
    );

        Debug.Log("Lobiye bildirilen port: " + serverPort);


        if (_lobbyManagerInstance != null && _currentLobbyID.IsValid())
        {
            _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
            ShowPanel(lobbyPanel);
        }

        uint serverIpCheck;
        ushort serverPortCheck;
        CSteamID serverSteamIdCheck;

        if (SteamMatchmaking.GetLobbyGameServer(_currentLobbyID, out serverIpCheck, out serverPortCheck, out serverSteamIdCheck))
        {
            Debug.Log($"[HOST] Steam'e kaydedilen sunucu bilgisi -> IP: {new System.Net.IPAddress(serverIpCheck)}, Port: {serverPortCheck}");
        }
        else
        {
            Debug.LogError("[HOST] Sunucu bilgileri Steam'e kaydedilemedi!");
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        Debug.Log($"Lobiye katılma isteği alındı. Lobi ID: {result.m_steamIDLobby}");
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        // Lobiye katılma isteği başarısız olursa
        if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"Lobiye katılamadı: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            ShowPanel(mainMenuPanel);
            return;
        }

        _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
        staticLobbyID = _currentLobbyID;

        // Eğer host değilsek (yani sunucu başlamadıysa) bir istemci olarak bağlanmalıyız.
        // Host zaten lobi oluşturduğunda sunucuyu ve istemciyi başlatmıştı.
        if (!networkManager.IsServerStarted)
        {
            uint serverIp;
            ushort serverPort;
            CSteamID serverSteamId;

            // Steam'den lobiye atanmış sunucu bilgilerini almaya çalış.
            if (SteamMatchmaking.GetLobbyGameServer(_currentLobbyID, out serverIp, out serverPort, out serverSteamId))
            {
                // Sunucu bilgileri başarıyla alındı.
                Debug.Log($"İstemci olarak lobiye bağlanılıyor... Sunucu IP: {new System.Net.IPAddress(serverIp)}, Port: {serverPort}");

                // FishySteamworks, lobiye bağlanırken bu bilgileri otomatik olarak kullanır.
                // Bu nedenle, sadece istemci bağlantısını başlatmak yeterlidir.
                networkManager.ClientManager.StartConnection();
            }
            else
            {
                Debug.LogError("Lobi için sunucu bilgisi (IP/Port) alınamadı. Bağlantı başlatılamıyor.");
                ShowPanel(mainMenuPanel);
                return;
            }
        }

        Debug.Log($"Lobiye katıldı! Lobi ID: {_currentLobbyID}");
        ShowPanel(lobbyPanel);
        _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID && lobbyPanel.activeInHierarchy)
        {
            _lobbyManagerInstance?.UpdatePlayerList();
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (this == null || lobbyPanel == null || !lobbyPanel.activeInHierarchy)
            return;

        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            if (_lobbyManagerInstance != null)
            {
                _lobbyManagerInstance.UpdatePlayerList();
            }
        }
    }
    public void LoadMainGameScene()
    {
        Debug.Log("Ana oyun sahnesi yükleniyor...");
        SceneLoadData sld = new SceneLoadData(mainGameSceneName)
        {
            ReplaceScenes = ReplaceOption.All
        };
        networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log("İstemci bağlantısı durdu.");
            ShowPanel(mainMenuPanel);
        }
    }

    private void OnServerRemoteConnectionStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Uzak istemci {conn.ClientId} ayrıldı.");
            _lobbyManagerInstance?.UpdatePlayerList();
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
            networkManager.ServerManager.OnRemoteConnectionState -= OnServerRemoteConnectionStateChanged;
        }

        LobbyCreated?.Dispose();
        GameLobbyJoinRequested?.Dispose();
        LobbyEntered?.Dispose();
        LobbyChatUpdate?.Dispose();
        LobbyDataUpdate?.Dispose();
    }
}