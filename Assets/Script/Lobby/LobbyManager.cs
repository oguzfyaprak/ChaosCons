using UnityEngine;
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Object;
using FishySteamworks;
using Steamworks;
using System.Text;
using UnityEngine.SceneManagement;
using FishNet.Managing.Scened;
using UnityEngine.UI; // Button sýnýfý için eklendi

public class LobbyManager : MonoBehaviour
{
    // --- Referanslar ---
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject readyButton; // Yeni eklendi: Hazýr butonu

    // Yüklenecek ana oyun sahnesinin adý (Inspector'dan atayýn)
    [SerializeField]
    private string mainGameSceneName = "MainMap";
    // Ana menü sahnesinin adý
    [SerializeField]
    private string mainMenuSceneName = "MainMenuScene";

    private NetworkManager _networkManager;
    private FishySteamworks.FishySteamworks _steamworksTransport;

    // --- Steamworks Callback'leri ---
    protected Callback<LobbyChatUpdate_t> LobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> LobbyDataUpdate;
    protected Callback<LobbyGameCreated_t> LobbyGameCreated;
    protected Callback<LobbyEnter_t> LobbyEntered;

    // --- Lobi Bilgisi ---
    private CSteamID _currentLobbyID;
    public static CSteamID staticLobbyID;

    void Awake()
    {
        _networkManager = FindFirstObjectByType<NetworkManager>();
        if (_networkManager == null)
        {
            Debug.LogError("LobbyManager: NetworkManager bulunamadý!");
            return;
        }

        _steamworksTransport = _networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (_steamworksTransport == null)
        {
            Debug.LogError("LobbyManager: FishySteamworks Transport bulunamadý!");
            return;
        }

        if (SteamManager.Initialized)
        {
            LobbyChatUpdate = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            LobbyDataUpdate = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            LobbyGameCreated = Callback<LobbyGameCreated_t>.Create(OnLobbyGameCreated);
            LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);
            Debug.Log("LobbyManager: Steamworks callback'leri kuruldu.");
        }
        else
        {
            Debug.LogError("LobbyManager: SteamAPI baþlatýlmadý!");
        }

        // --- Butonlara Listener Ekle ---
        if (startGameButton != null)
        {
            var button = startGameButton.GetComponent<Button>(); // UnityEngine.UI.Button kullanýldý
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("StartGameButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        if (leaveLobbyButton != null)
        {
            var button = leaveLobbyButton.GetComponent<Button>(); // UnityEngine.UI.Button kullanýldý
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LeaveLobbyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        // Yeni eklendi: Hazýr butonu için listener
        if (readyButton != null)
        {
            var button = readyButton.GetComponent<Button>(); // UnityEngine.UI.Button kullanýldý
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("ReadyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
    }

    void Start()
    {
        _currentLobbyID = staticLobbyID;

        // Baþlangýçta her iki butonu da gizle, InitializeLobbyUI belirleyecek
        if (startGameButton != null) startGameButton.SetActive(false);
        if (readyButton != null) readyButton.SetActive(false);

        if (_currentLobbyID.IsValid())
        {
            Debug.Log($"LobbyManager: Start'ta geçerli lobi ID'si bulundu: {_currentLobbyID}");
            InitializeLobbyUI(_currentLobbyID);
        }
        else
        {
            Debug.LogWarning("LobbyManager: Start'ta geçerli lobi ID'si bulunamadý. Callback bekleniyor.");
        }
    }

    private void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (lobbyIdText != null)
        {
            lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
        }
        UpdatePlayerList(); // UI güncellendiðinde oyuncu listesini de güncelle

        // Host ise oyunu baþlat butonunu aktif et, hazýr butonunu gizle
        if (_networkManager.IsServerStarted && SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID())
        {
            if (startGameButton != null) startGameButton.SetActive(true);
            if (readyButton != null) readyButton.SetActive(false);
        }
        // Client ise hazýr butonunu aktif et, oyunu baþlat butonunu gizle
        else
        {
            if (startGameButton != null) startGameButton.SetActive(false);
            if (readyButton != null) readyButton.SetActive(true);
            // Client'ýn hazýr durumunu kontrol et ve butonu güncelle
            UpdateReadyButtonState();
        }
    }

    // --- UI Buton Fonksiyonlarý ---
    public void OnClick_StartGame()
    {
        // Sadece host oyunu baþlatabilir ve tüm oyuncular hazýr olmalý
        if (!_networkManager.IsServerStarted || SteamMatchmaking.GetLobbyOwner(_currentLobbyID) != SteamUser.GetSteamID())
        {
            Debug.LogWarning("Sadece lobi host'u oyunu baþlatabilir!");
            return;
        }

        // Tüm oyuncularýn hazýr olup olmadýðýný kontrol et
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("Tüm oyuncular hazýr deðil! Oyun baþlatýlamaz.");
            return;
        }

        Debug.Log("Oyunu baþlatýlýyor... Ana oyun sahnesine geçiliyor.");

        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        SceneLoadData sld = new SceneLoadData(mainGameSceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    public void OnClick_LeaveLobby()
    {
        Debug.Log("Lobiden ayrýlýyor...");
        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            Debug.Log($"Lobiden ayrýlýndý: {_currentLobbyID}");
            staticLobbyID = CSteamID.Nil;
        }

        if (_networkManager.IsServerStarted)
        {
            _networkManager.ServerManager.StopConnection(true);
        }
        else if (_networkManager.IsClientStarted)
        {
            _networkManager.ClientManager.StopConnection();
        }
        _steamworksTransport.StopConnection(false);

        SceneLoadData sld = new SceneLoadData(mainMenuSceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    // Yeni eklendi: Hazýr butonu fonksiyonu
    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        // Kendi hazýr durumumuzu lobi üyesi verisi olarak ayarla
        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        // Hazýr durumunu tersine çevir
        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Hazýr durumu güncellendi: {newReadyStatus}");
        UpdateReadyButtonState(); // Butonun metnini güncelle
        UpdatePlayerList(); // Oyuncu listesini güncelle
    }

    // Hazýr butonunun metnini ve durumunu güncelleyen yardýmcý metot
    private void UpdateReadyButtonState()
    {
        if (readyButton == null) return;

        Button button = readyButton.GetComponent<Button>();
        if (button == null) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        TMP_Text buttonText = readyButton.GetComponentInChildren<TMP_Text>();
        if (buttonText != null)
        {
            buttonText.text = isCurrentlyReady ? "Hazýr (Bekle)" : "Hazýr Ol";
        }
    }


    // --- Steamworks Callback Implementasyonlarý ---
    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"Lobi Sohbet Güncellemesi: {pCallback.m_ulSteamIDUserChanged} için {pCallback.m_rgfChatMemberStateChange}");
            UpdatePlayerList();
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"Lobi Veri Güncellemesi: {pCallback.m_ulSteamIDLobby}");
            UpdatePlayerList();

            // Eðer oyun baþladýysa ve ben host deðilsem, oyun sahnesine geç
            if (!_networkManager.IsServerStarted && SteamMatchmaking.GetLobbyData(_currentLobbyID, "GameStarted") == "true")
            {
                Debug.Log("Lobi host'u oyunu baþlattý. Ana oyun sahnesine geçiliyor.");
                SteamMatchmaking.LeaveLobby(_currentLobbyID);
                SceneLoadData sld = new SceneLoadData(mainGameSceneName);
                sld.ReplaceScenes = ReplaceOption.All;
                _networkManager.SceneManager.LoadGlobalScenes(sld);
            }
        }
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.Log($"LobbyManager: Steam Lobisine baþarýyla katýldý! Lobi ID: {result.m_ulSteamIDLobby}");
            InitializeLobbyUI(new CSteamID(result.m_ulSteamIDLobby));
            // Lobiye girerken kendi hazýr durumumuzu "false" olarak ayarla
            SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", "false");
        }
        else
        {
            Debug.LogError($"LobbyManager: Steam Lobisine katýlamadý: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            SceneLoadData sld = new SceneLoadData(mainMenuSceneName);
            sld.ReplaceScenes = ReplaceOption.All;
            _networkManager.SceneManager.LoadGlobalScenes(sld);
            if (_networkManager.IsServerStarted) _networkManager.ServerManager.StopConnection(true);
            else if (_networkManager.IsClientStarted) _networkManager.ClientManager.StopConnection();
            _steamworksTransport.StopConnection(false);
        }
    }

    private void OnLobbyGameCreated(LobbyGameCreated_t result)
    {
        Debug.Log($"Lobi için oyun oluþturuldu. Sunucu IP: {result.m_unIP}, Port: {result.m_usPort}, Lobi ID: {result.m_ulSteamIDLobby}");
    }

    // --- Yardýmcý Fonksiyonlar ---
    private void UpdatePlayerList()
    {
        if (!_currentLobbyID.IsValid() || playerListText == null) return;

        StringBuilder sb = new StringBuilder("Oyuncular:\n");
        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string personaName;

            if (memberSteamID.IsValid())
            {
                if (memberSteamID == SteamUser.GetSteamID())
                {
                    personaName = SteamFriends.GetPersonaName();
                }
                else
                {
                    personaName = SteamFriends.GetFriendPersonaName(memberSteamID);
                }
            }
            else
            {
                personaName = "Bilinmeyen Oyuncu";
                Debug.LogWarning($"Geçersiz Steam ID bulundu: {memberSteamID}");
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";

            // Oyuncunun hazýr durumunu al
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = (readyStatus == "true") ? " (Hazýr)" : " (Hazýr Deðil)";

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        playerListText.text = sb.ToString();
    }

    // Yeni eklendi: Tüm oyuncularýn hazýr olup olmadýðýný kontrol eden metot
    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        // Lobi sahibi tek baþýna ise ve baþka oyuncu yoksa, her zaman hazýr sayýlýr.
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Host'u hazýr kontrolünden muaf tutabiliriz, çünkü host oyunu baþlatýr.
            // Ancak genellikle host'un da hazýr olmasý beklenir.
            // Bu örnekte host da dahil tüm oyuncularýn hazýr olmasý kontrol ediliyor.
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false; // Hazýr olmayan bir oyuncu bulundu
            }
        }
        return true; // Tüm oyuncular hazýr
    }
}
