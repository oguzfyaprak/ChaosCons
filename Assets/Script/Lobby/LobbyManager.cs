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
using UnityEngine.UI; // Button s�n�f� i�in eklendi

public class LobbyManager : MonoBehaviour
{
    // --- Referanslar ---
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject readyButton; // Yeni eklendi: Haz�r butonu

    // Y�klenecek ana oyun sahnesinin ad� (Inspector'dan atay�n)
    [SerializeField]
    private string mainGameSceneName = "MainMap";
    // Ana men� sahnesinin ad�
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
            Debug.LogError("LobbyManager: NetworkManager bulunamad�!");
            return;
        }

        _steamworksTransport = _networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (_steamworksTransport == null)
        {
            Debug.LogError("LobbyManager: FishySteamworks Transport bulunamad�!");
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
            Debug.LogError("LobbyManager: SteamAPI ba�lat�lmad�!");
        }

        // --- Butonlara Listener Ekle ---
        if (startGameButton != null)
        {
            var button = startGameButton.GetComponent<Button>(); // UnityEngine.UI.Button kullan�ld�
            if (button != null) button.onClick.AddListener(OnClick_StartGame);
            else Debug.LogWarning("StartGameButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        if (leaveLobbyButton != null)
        {
            var button = leaveLobbyButton.GetComponent<Button>(); // UnityEngine.UI.Button kullan�ld�
            if (button != null) button.onClick.AddListener(OnClick_LeaveLobby);
            else Debug.LogWarning("LeaveLobbyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
        // Yeni eklendi: Haz�r butonu i�in listener
        if (readyButton != null)
        {
            var button = readyButton.GetComponent<Button>(); // UnityEngine.UI.Button kullan�ld�
            if (button != null) button.onClick.AddListener(OnClick_Ready);
            else Debug.LogWarning("ReadyButton �zerinde UnityEngine.UI.Button bile�eni bulunamad�.");
        }
    }

    void Start()
    {
        _currentLobbyID = staticLobbyID;

        // Ba�lang��ta her iki butonu da gizle, InitializeLobbyUI belirleyecek
        if (startGameButton != null) startGameButton.SetActive(false);
        if (readyButton != null) readyButton.SetActive(false);

        if (_currentLobbyID.IsValid())
        {
            Debug.Log($"LobbyManager: Start'ta ge�erli lobi ID'si bulundu: {_currentLobbyID}");
            InitializeLobbyUI(_currentLobbyID);
        }
        else
        {
            Debug.LogWarning("LobbyManager: Start'ta ge�erli lobi ID'si bulunamad�. Callback bekleniyor.");
        }
    }

    private void InitializeLobbyUI(CSteamID lobbyID)
    {
        _currentLobbyID = lobbyID;
        if (lobbyIdText != null)
        {
            lobbyIdText.text = $"Lobi ID: {_currentLobbyID.m_SteamID}";
        }
        UpdatePlayerList(); // UI g�ncellendi�inde oyuncu listesini de g�ncelle

        // Host ise oyunu ba�lat butonunu aktif et, haz�r butonunu gizle
        if (_networkManager.IsServerStarted && SteamMatchmaking.GetLobbyOwner(_currentLobbyID) == SteamUser.GetSteamID())
        {
            if (startGameButton != null) startGameButton.SetActive(true);
            if (readyButton != null) readyButton.SetActive(false);
        }
        // Client ise haz�r butonunu aktif et, oyunu ba�lat butonunu gizle
        else
        {
            if (startGameButton != null) startGameButton.SetActive(false);
            if (readyButton != null) readyButton.SetActive(true);
            // Client'�n haz�r durumunu kontrol et ve butonu g�ncelle
            UpdateReadyButtonState();
        }
    }

    // --- UI Buton Fonksiyonlar� ---
    public void OnClick_StartGame()
    {
        // Sadece host oyunu ba�latabilir ve t�m oyuncular haz�r olmal�
        if (!_networkManager.IsServerStarted || SteamMatchmaking.GetLobbyOwner(_currentLobbyID) != SteamUser.GetSteamID())
        {
            Debug.LogWarning("Sadece lobi host'u oyunu ba�latabilir!");
            return;
        }

        // T�m oyuncular�n haz�r olup olmad���n� kontrol et
        if (!AreAllPlayersReady())
        {
            Debug.LogWarning("T�m oyuncular haz�r de�il! Oyun ba�lat�lamaz.");
            return;
        }

        Debug.Log("Oyunu ba�lat�l�yor... Ana oyun sahnesine ge�iliyor.");

        SteamMatchmaking.SetLobbyData(_currentLobbyID, "GameStarted", "true");

        SceneLoadData sld = new SceneLoadData(mainGameSceneName);
        sld.ReplaceScenes = ReplaceOption.All;
        _networkManager.SceneManager.LoadGlobalScenes(sld);
    }

    public void OnClick_LeaveLobby()
    {
        Debug.Log("Lobiden ayr�l�yor...");
        if (_currentLobbyID.IsValid())
        {
            SteamMatchmaking.LeaveLobby(_currentLobbyID);
            Debug.Log($"Lobiden ayr�l�nd�: {_currentLobbyID}");
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

    // Yeni eklendi: Haz�r butonu fonksiyonu
    public void OnClick_Ready()
    {
        if (!_currentLobbyID.IsValid()) return;

        // Kendi haz�r durumumuzu lobi �yesi verisi olarak ayarla
        string currentReadyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, SteamUser.GetSteamID(), "ReadyStatus");
        bool isCurrentlyReady = (currentReadyStatus == "true");

        // Haz�r durumunu tersine �evir
        string newReadyStatus = isCurrentlyReady ? "false" : "true";
        SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", newReadyStatus);

        Debug.Log($"Haz�r durumu g�ncellendi: {newReadyStatus}");
        UpdateReadyButtonState(); // Butonun metnini g�ncelle
        UpdatePlayerList(); // Oyuncu listesini g�ncelle
    }

    // Haz�r butonunun metnini ve durumunu g�ncelleyen yard�mc� metot
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
            buttonText.text = isCurrentlyReady ? "Haz�r (Bekle)" : "Haz�r Ol";
        }
    }


    // --- Steamworks Callback Implementasyonlar� ---
    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"Lobi Sohbet G�ncellemesi: {pCallback.m_ulSteamIDUserChanged} i�in {pCallback.m_rgfChatMemberStateChange}");
            UpdatePlayerList();
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"Lobi Veri G�ncellemesi: {pCallback.m_ulSteamIDLobby}");
            UpdatePlayerList();

            // E�er oyun ba�lad�ysa ve ben host de�ilsem, oyun sahnesine ge�
            if (!_networkManager.IsServerStarted && SteamMatchmaking.GetLobbyData(_currentLobbyID, "GameStarted") == "true")
            {
                Debug.Log("Lobi host'u oyunu ba�latt�. Ana oyun sahnesine ge�iliyor.");
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
            Debug.Log($"LobbyManager: Steam Lobisine ba�ar�yla kat�ld�! Lobi ID: {result.m_ulSteamIDLobby}");
            InitializeLobbyUI(new CSteamID(result.m_ulSteamIDLobby));
            // Lobiye girerken kendi haz�r durumumuzu "false" olarak ayarla
            SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", "false");
        }
        else
        {
            Debug.LogError($"LobbyManager: Steam Lobisine kat�lamad�: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
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
        Debug.Log($"Lobi i�in oyun olu�turuldu. Sunucu IP: {result.m_unIP}, Port: {result.m_usPort}, Lobi ID: {result.m_ulSteamIDLobby}");
    }

    // --- Yard�mc� Fonksiyonlar ---
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
                Debug.LogWarning($"Ge�ersiz Steam ID bulundu: {memberSteamID}");
            }

            string hostIndicator = (memberSteamID == SteamMatchmaking.GetLobbyOwner(_currentLobbyID)) ? " (Host)" : "";

            // Oyuncunun haz�r durumunu al
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            string readyIndicator = (readyStatus == "true") ? " (Haz�r)" : " (Haz�r De�il)";

            sb.AppendLine($"- {personaName}{hostIndicator}{readyIndicator}");
        }

        playerListText.text = sb.ToString();
    }

    // Yeni eklendi: T�m oyuncular�n haz�r olup olmad���n� kontrol eden metot
    private bool AreAllPlayersReady()
    {
        if (!_currentLobbyID.IsValid()) return false;

        int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
        // Lobi sahibi tek ba��na ise ve ba�ka oyuncu yoksa, her zaman haz�r say�l�r.
        if (numMembers <= 1) return true;

        for (int i = 0; i < numMembers; i++)
        {
            CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
            // Host'u haz�r kontrol�nden muaf tutabiliriz, ��nk� host oyunu ba�lat�r.
            // Ancak genellikle host'un da haz�r olmas� beklenir.
            // Bu �rnekte host da dahil t�m oyuncular�n haz�r olmas� kontrol ediliyor.
            string readyStatus = SteamMatchmaking.GetLobbyMemberData(_currentLobbyID, memberSteamID, "ReadyStatus");
            if (readyStatus != "true")
            {
                return false; // Haz�r olmayan bir oyuncu bulundu
            }
        }
        return true; // T�m oyuncular haz�r
    }
}
