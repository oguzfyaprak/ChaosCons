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
        // Lobi oluşturma isteği başarısız olursa
        if (result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"Lobi oluşturma hatası: {result.m_eResult}");
            ShowPanel(mainMenuPanel);
            return;
        }

        _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
        staticLobbyID = _currentLobbyID;

        Debug.Log($"Lobi oluşturuldu. Lobi ID: {_currentLobbyID}");

        // Host olarak hem sunucu hem de istemci bağlantılarını başlat.
        // FishySteamworks P2P modunda, bu çağrılar otomatik olarak Steam ID'sini kullanarak
        // doğru şekilde bağlanacaktır.
        networkManager.ServerManager.StartConnection();
        networkManager.ClientManager.StartConnection();

        // Bu noktada, Steam'e IP ve port bildirmek yerine, sadece lobiye bir "game server" olduğunu
        // ve bu server'ın kendi Steam ID'sine sahip olduğunu bildirmek yeterli olabilir.
        // Eğer FishySteamworks P2P kullanıyorsanız, bu satırı yorum satırı olarak bırakmak
        // en doğru yaklaşım olacaktır. FishySteamworks'ün kendisi bu durumu zaten yönetir.

        // SteamMatchmaking.SetLobbyGameServer(
        //     _currentLobbyID,
        //     0, // IP adresi gerekmez
        //     0, // Port numarası gerekmez
        //     SteamUser.GetSteamID() // Sunucunun Steam ID'sini bildirir
        // );

        // FishySteamworks port bilgisini manuel olarak yazdığımız için aşağıdaki satırı ekledim.
        // Bu satır, lobinin portunu FishySteamworks'ün ayarlarından alarak Steam'e bildirir.
        // Daha önceki sorununuzda portun 0 olmasının sebebi, GetPort()'un yanlış değer dönmesiydi.
        // Bu yüzden bu satırda elle atama yapmak daha güvenli.
        ushort serverPort = 7777; // Inspector'daki port numaranızı buraya yazın.
        SteamMatchmaking.SetLobbyGameServer(
            _currentLobbyID,
            0, // P2P için IP 0 olarak kalabilir
            serverPort, // Elle atanan port numarasını kullanın
            SteamUser.GetSteamID()
        );

        Debug.Log($"[HOST] Steam'e bildirilen port: {serverPort}");

        ShowPanel(lobbyPanel);
        _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
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