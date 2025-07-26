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
    [SerializeField] private GameObject readyButton;

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
    protected Callback<PersonaStateChange_t> PersonaStateChange; // Yeni eklendi: Oyuncu adý güncellemelerini dinlemek için

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
            PersonaStateChange = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange); // Yeni eklendi
            Debug.Log("LobbyManager: Steamworks callback'leri kuruldu.");
        }
        else
        {
            Debug.LogError("LobbyManager: SteamAPI baþlatýlmadý!");
        }

        if (startGameButton != null)
        {
            var button = startGameButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("StartGameButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        if (leaveLobbyButton != null)
        {
            var button = leaveLobbyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LeaveLobbyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
        if (readyButton != null)
        {
            var button = readyButton.GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("ReadyButton üzerinde UnityEngine.UI.Button bileþeni bulunamadý.");
        }
    }

    void Start()
    {
        _currentLobbyID = staticLobbyID;

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
        UpdatePlayerList();

        if (_networkManager.IsServerStarted && SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID())
        {
            if (startGameButton != null) startGameButton.SetActive(true);
            if (readyButton != null) readyButton.SetActive(false);
        }
        else
        {
            if (startGameButton != null) startGameButton.SetActive(false);
            if (readyButton != null) readyButton.SetActive(true);
            UpdateReadyButtonState();
        }
    }

    public void OnClick_StartGame()
    {
        var localSteamId = SteamUser.GetSteamID();
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(_currentLobbyID);
        Debug.Log($"[StartGame] LocalSteamID: {localSteamId}, LobbyOwner: {lobbyOwner}");

        if (!_networkManager.IsServerStarted || lobbyOwner != localSteamId)
        {
            Debug.LogWarning("Sadece lobi host'u oyunu baþlatabilir!");
            return;
        }

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

        // !!! BURADA BEKLE !!! Coroutine ile delay koyabilirsin veya Invoke("LoadMenu", 0.5f) gibi çözebilirsin.
        Invoke(nameof(LoadMenuScene), 0.25f); // delay ekle

        // Alternatif olarak sahne yüklemeyi tamamen coroutine ile yapabilirsin
    }

    private void LoadMenuScene()
    {
        SceneLoadData sld = new SceneLoadData(mainMenuSceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Hazýr durumu güncellendi: {newReadyStatus}");
        UpdateReadyButtonState();
        UpdatePlayerList();
    }

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

            // Lobiye girerken tüm mevcut üyelerin bilgilerini talep et
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
                // Bilgi zaten önbelleðe alýnmýþsa false döndürür, aksi takdirde true döndürür ve bilgiyi talep eder.
                SteamFriends.RequestUserInformation(memberSteamID, false);
            }

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

    private void OnPersonaStateChange(PersonaStateChange_t pCallback) // Yeni callback metodu
    {
        // Bir oyuncunun persona durumu (adý, avatarý vb.) deðiþtiðinde tetiklenir.
        // Bu, RequestUserInformation çaðrýsý sonrasý bilginin geldiðini gösterir.
        Debug.Log($"LobbyManager: Persona durumu deðiþti: {pCallback.m_ulSteamID} için {pCallback.m_nChangeFlags}");
        // Eðer deðiþen kiþi mevcut lobi üyelerinden biriyse, listeyi güncelle.
        if (_currentLobbyID.IsValid())
        {
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
                if (memberSteamID.m_SteamID == pCallback.m_ulSteamID)
                {
                    UpdatePlayerList(); // Ýlgili oyuncu listesini güncelle
                    break;
                }
            }
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
                // Kendi Steam ID'miz ise SteamFriends.GetPersonaName() kullanýn.
                if (memberSteamID == SteamUser.GetSteamID())
                {
                    personaName = SteamFriends.GetPersonaName(); // Argüman almayan kendi adýmýzý alma metodu
                }
                else // Diðer oyuncular için GetFriendPersonaName() kullanýn.
                {
                    try
                    {
                        personaName = SteamFriends.GetFriendPersonaName(memberSteamID);
                        if (string.IsNullOrEmpty(personaName) || personaName == "[unknown]")
                        {
                            personaName = "Yükleniyor...";
                            SteamFriends.RequestUserInformation(memberSteamID, false);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"LobbyManager: Oyuncu adý alýnýrken hata oluþtu ({memberSteamID}): {ex.Message}");
                        personaName = "Ad Alýnamadý (Hata)";
                        SteamFriends.RequestUserInformation(memberSteamID, false);
                    }
                }
            }
            else
            {
                personaName = "Bilinmeyen Oyuncu";
                Debug.LogWarning($"Geçersiz Steam ID bulundu: {memberSteamID}");
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";

            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = (readyStatus == "true") ? " (Hazýr)" : " (Hazýr Deðil)";

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        playerListText.text = sb.ToString();
    }

    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false;
            }
        }
        return true;
    }
}
