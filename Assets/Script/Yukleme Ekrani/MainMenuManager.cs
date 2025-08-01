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
    // --- Referanslar ---
    public NetworkManager networkManager;
    private FishySteamworks.FishySteamworks steamworksTransportInstance;

    // UI Panelleri
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbySelectionPanel;
    [SerializeField] private GameObject joinLobbyPanel;
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private GameObject settingsPanel;

    // Lobi ID ile Katıl Paneli Elemanları
    [SerializeField] private TMP_InputField lobbyIdInputField;

    // Lobi Paneli Elemanları (LobbyManager tarafından erişilecek)
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject readyButton;

    // Yüklenecek ana oyun sahnesinin adı
    [SerializeField] private string mainGameSceneName = "MainMap";
    // Ana menü sahnesinin adı
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";

    [SerializeField] private NetworkManager _networkManager;

    // Steam API'den gelen Callback'ler için tutucular
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> LobbyEntered;
    protected Callback<LobbyChatUpdate_t> LobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> LobbyDataUpdate;
    protected Callback<LobbyGameCreated_t> LobbyGameCreated;
    protected Callback<PersonaStateChange_t> PersonaStateChange;

    // Lobi Bilgisi
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
                Debug.LogError("MainMenuManager: NetworkManager bulunamadı!");
                return;
            }
        }

        _networkManager = FindFirstObjectByType<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("❌ Sahne içerisinde NetworkManager bulunamadı!");
        }

        steamworksTransportInstance = networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (steamworksTransportInstance == null)
        {
            Debug.LogError("MainMenuManager: FishySteamworks Transport bulunamadı!");
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
            LobbyGameCreated = Callback<LobbyGameCreated_t>.Create(OnLobbyGameCreated);
            PersonaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);

            Debug.Log($"MainMenuManager: Steamworks.NET ve FishySteamworks için callback'ler kuruldu. Oyuncu Adı: {SteamFriends.GetPersonaName()}, SteamID: {SteamUser.GetSteamID().m_SteamID}");
        }
        else
        {
            Debug.LogError("MainMenuManager: SteamAPI başlatılamadı! Steam açık mı ve steam_appid.txt doğru mu?");
        }

        networkManager.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;
        networkManager.ServerManager.OnRemoteConnectionState += OnServerRemoteConnectionStateChanged;
        networkManager.ServerManager.OnServerConnectionState += OnServerConnectionStateChanged;

        _lobbyManagerInstance = GetComponent<LobbyManager>();
        if (_lobbyManagerInstance == null)
        {
            Debug.LogError("MainMenuManager: LobbyManager bulunamadı! Lütfen bu GameObject'e bir LobbyManager bileşeni ekleyin.");
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
        mainMenuPanel.SetActive(false);
        lobbySelectionPanel.SetActive(false);
        joinLobbyPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        settingsPanel.SetActive(false);

        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }
    }

    // --- BUTON FONKSİYONLARI ---
    public void OnClick_PlayGame() => ShowPanel(lobbySelectionPanel);
    public void OnClick_Ayarlar() => ShowPanel(settingsPanel);
    public void OnClick_Hakkimizda() => Debug.Log("MainMenuManager: Hakkımızda açıldı.");
    public void OnClick_OyundanCik()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    public void OnClick_Feedback() => Application.OpenURL("https://www.orneksite.com/geribildirim");
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

    // --- STEAM CALLBACK'LERİ ---

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        // Oluşturma başarılı değilse hata logu göster.
        if (result.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError($"❌ Steam lobisi oluşturma hatası: {result.m_eResult}");
            return;
        }

        // Lobi ID'sini sakla ve lobiye bağlan.
        _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
        Debug.Log($"✅ Steam lobisi başarıyla oluşturuldu! ID: {_currentLobbyID.m_SteamID}");

        // Yeni bir lobi oluşturulduğunda, host hem sunucu hem de istemci olarak başlar.
        _networkManager.ServerManager.StartConnection();
        _networkManager.ClientManager.StartConnection();

        // HATA GİDERME: UI Manager'ın varlığını kontrol et.
        if (_lobbyManagerInstance != null && _currentLobbyID.IsValid())
        {
            // UI'ı başlat ve lobi bilgilerini göster.
            _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
        }
        else
        {
            Debug.LogError("Lobi yöneticisi başlatılamadı veya lobi ID geçersiz!");
        }
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        Debug.Log($"Lobiye katılma isteği alındı. Lobi ID: {result.m_steamIDLobby}");
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.LogError($"Steam Lobisine katılamadı: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            HandleConnectionFailure();
            ShowPanel(mainMenuPanel);
            return;
        }

        _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
        staticLobbyID = _currentLobbyID;

        // Host olmadığımız için sadece Client'ı başlatıyoruz.
        if (!networkManager.IsServerStarted)
        {
            networkManager.ClientManager.StartConnection();
        }

        Debug.Log($"Steam Lobisine başarıyla katıldı! Lobi ID: {_currentLobbyID}");
        ShowPanel(lobbyPanel);
        _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log("Lobby Chat Update: Oyuncu listesi güncelleniyor.");
            // --- İYİLEŞTİRME: Lobi paneli aktifse güncelleme yap ---
            if (_lobbyManagerInstance != null && lobbyPanel.activeInHierarchy)
            {
                _lobbyManagerInstance.UpdatePlayerList();
            }
        }
    }

    // MainMenuManager.cs
    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        // Sadece mevcut lobiye ait verilerin güncellendiğinden emin ol.
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log("Lobby Data Update: Lobi UI güncelleniyor.");

            // HATA GİDERME: UI Manager ve lobi panelinin varlığını kontrol et.
            // Bu, `LobbyManager` yoksa veya UI pasifse kodun çökmesini önler.
            if (_lobbyManagerInstance != null && lobbyPanel != null && lobbyPanel.activeInHierarchy)
            {
                // Oyuncu listesini ve hazır butonunu güncelle.
                _lobbyManagerInstance.UpdatePlayerList();
                _lobbyManagerInstance.UpdateReadyButtonState();
            }
            else
            {
                Debug.LogWarning("Lobi UI'ı aktif olmadığı için güncelleme pas geçildi.");
            }
        }
    }

    private void OnLobbyGameCreated(LobbyGameCreated_t result)
    {
        Debug.Log($"Lobi için oyun oluşturuldu. Sunucu IP: {result.m_unIP}, Port: {result.m_usPort}, Lobi ID: {result.m_ulSteamIDLobby}");
    }

    private void OnPersonaStateChange(PersonaStateChange_t pCallback)
    {
        Debug.Log($"Persona durumu değişti: {pCallback.m_ulSteamID}");

        // Lobideyseniz ve LobbyManager referansı varsa, oyuncu listesini güncelle.
        if (_currentLobbyID.IsValid() && _lobbyManagerInstance != null)
        {
            // Bu satırın mevcut olduğundan emin olun.
            _lobbyManagerInstance.UpdatePlayerList();
        }
    }
    // --- BAĞLANTI YÖNETİMİ VE DİĞER FONKSİYONLAR ---

    private void HandleConnectionFailure()
    {
        Debug.Log("Bağlantı hatası/kesilmesi durumu işleniyor...");
        if (networkManager.IsServerStarted) networkManager.ServerManager.StopConnection(true);
        if (networkManager.IsClientStarted) networkManager.ClientManager.StopConnection();
        staticLobbyID = CSteamID.Nil;
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
    private IEnumerator ReinitializeLobbyManagerAfterSceneLoad()
    {
        yield return new WaitForSeconds(1f); // Sahne yüklenmesini bekle

        LobbyManager lm = Object.FindAnyObjectByType<LobbyManager>();
        if (lm != null)
        {
            Debug.Log("Yeni sahnedeki LobbyManager bulundu, yeniden başlatılıyor...");

            // Yeni sahnedeki UI nesnelerini bul (GameObject isimleri sahnedeki objelere birebir uymalı!)
            TMP_Text newLobbyIdText = GameObject.Find("LobbyIdText")?.GetComponent<TMP_Text>();
            TMP_Text newPlayerListText = GameObject.Find("PlayerListText")?.GetComponent<TMP_Text>();
            GameObject newStartGameButton = GameObject.Find("StartGameButton");
            GameObject newLeaveLobbyButton = GameObject.Find("LeaveLobbyButton");
            GameObject newReadyButton = GameObject.Find("ReadyButton");

            if (newLobbyIdText == null || newPlayerListText == null)
            {
                Debug.LogError("❌ Yeni sahnede UI referansları bulunamadı! Lütfen sahnedeki GameObject adlarını kontrol et: 'LobbyIdText', 'PlayerListText' vs.");
                yield break;
            }

            // Initialize çağrısı (yeni referanslarla)
            lm.Initialize(
                networkManager,
                steamworksTransportInstance,
                newLobbyIdText,
                newPlayerListText,
                newStartGameButton,
                newLeaveLobbyButton,
                newReadyButton,
                mainGameSceneName
            );

            lm.InitializeLobbyUI(staticLobbyID);
        }
        else
        {
            Debug.LogError("Yeni sahnede LobbyManager bulunamadı!");
        }
    }


    private void OnClientConnectionStateChanged(ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log($"FishNet İstemci Bağlantısı Durdu.");
            HandleConnectionFailure();
            ShowPanel(mainMenuPanel);
        }
    }

    private void OnServerRemoteConnectionStateChanged(NetworkConnection conn, RemoteConnectionStateArgs args)
    {
        if (args.ConnectionState == RemoteConnectionState.Stopped)
        {
            Debug.Log($"Uzak İstemci {conn.ClientId} ayrıldı.");
            if (_lobbyManagerInstance != null && lobbyPanel.activeInHierarchy)
            {
                _lobbyManagerInstance.UpdatePlayerList();
            }
        }
    }

    private void OnServerConnectionStateChanged(ServerConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Stopped)
        {
            Debug.Log($"FishNet Sunucu Bağlantısı Durdu.");
            HandleConnectionFailure();
            ShowPanel(mainMenuPanel);
        }
    }

    void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
            networkManager.ServerManager.OnRemoteConnectionState -= OnServerRemoteConnectionStateChanged;
            networkManager.ServerManager.OnServerConnectionState -= OnServerConnectionStateChanged;
        }

        // Steamworks callback'lerini kaldır
        LobbyCreated?.Dispose();
        GameLobbyJoinRequested?.Dispose();
        LobbyEntered?.Dispose();
        LobbyChatUpdate?.Dispose();
        LobbyDataUpdate?.Dispose();
        LobbyGameCreated?.Dispose();
        PersonaStateChange?.Dispose();
    }
}