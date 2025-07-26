using UnityEngine;
using UnityEngine.SceneManagement; // Unity'nin SceneManager'ı ve LoadSceneMode için
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using FishNet.Managing.Scened; // FishNet'in SceneManager ve SceneLoadData için

public class MainMenuManager : MonoBehaviour
{
    public NetworkManager networkManager;
    private FishySteamworks.FishySteamworks steamworksTransportInstance;

    // UI Panelleri
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject lobbyJoinPanel;
    [SerializeField] private GameObject settingsPanel; // Yeni eklendi: Ayarlar paneli
    [SerializeField] private TMP_InputField lobbyIdInputField; // Sadece lobbyJoinPanel içindeki input field

    // Yüklenecek lobi sahnesinin adı (Inspector'dan atayın)
    [SerializeField]
    private string lobbySceneName = "LobbyScene"; // Unity Inspector'dan doğru sahne adını atadığınızdan emin olun

    // Steam API'den gelen Callback'ler için tutucular
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> LobbyEntered;

    void Awake()
    {
        // 1. NetworkManager'ı bul ve ata.
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("MainMenuManager: NetworkManager bulunamadı! Lütfen sahnede bir NetworkManager olduğundan emin olun.");
                return;
            }
        }

        // 2. NetworkManager'ın kullandığı FishySteamworks Transport'unu al.
        steamworksTransportInstance = networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (steamworksTransportInstance == null)
        {
            Debug.LogError("MainMenuManager: FishySteamworks Transport bulunamadı! NetworkManager ile aynı GameObject üzerinde FishySteamworks bileşeni olduğundan emin olun.");
            return;
        }

        // 3. UI panellerini başlangıçta ayarla
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false); // Yeni eklendi: Ayarlar paneli başlangıçta kapalı

        // 4. Steamworks API başlatıldı mı kontrol et ve callback'leri ayarla.
        if (SteamManager.Initialized)
        {
            LobbyCreated = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
            GameLobbyJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            LobbyEntered = Callback<LobbyEnter_t>.Create(OnLobbyEntered);

            Debug.Log("MainMenuManager: Steamworks.NET ve FishySteamworks için callback'ler kuruldu.");
            Debug.Log($"Oyuncu Adı: {SteamFriends.GetPersonaName()}, SteamID: {SteamUser.GetSteamID().m_SteamID}");
        }
        else
        {
            Debug.LogError("MainMenuManager: SteamAPI başlatılamadı! Steam açık mı ve steam_appid.txt doğru mu?");
        }
    }

    // --- Ana Menü Buton Fonksiyonları ---

    public void OnClick_PlayGame()
    {
        Debug.Log("MainMenuManager: Lobi oluşturuluyor...");
        if (steamworksTransportInstance != null)
        {
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        }
        else
        {
            Debug.LogError("MainMenuManager: FishySteamworks Transport referansı boş!");
        }
    }

    public void OnClick_JoinGame() // "OYUNA KATIL" (Lobi ID ile katılma paneli aç)
    {
        Debug.Log("MainMenuManager: Lobiye Katıl ekranı açılıyor.");
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false); // Diğer panelleri kapat
    }

    public void OnClick_Ayarlar() // Yeni eklendi: Ayarlar butonu
    {
        Debug.Log("MainMenuManager: Ayarlar ekranı açılıyor.");
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(true); // Ayarlar panelini aç
    }

    public void OnClick_Hakkimizda() { Debug.Log("MainMenuManager: Hakkımızda açıldı."); }
    public void OnClick_OyundanCik()
    {
        Debug.Log("MainMenuManager: Oyundan çıkılıyor...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
    public void OnClick_Feedback() { Application.OpenURL("https://www.orneksite.com/geribildirim"); }

    // --- Lobiye Katıl Paneli Fonksiyonları ---

    public void OnClick_JoinLobbyById() // Lobi ID'si ile Katıl butonu
    {
        string lobbyIdString = lobbyIdInputField.text;
        if (string.IsNullOrEmpty(lobbyIdString))
        {
            Debug.LogError("MainMenuManager: Lobi ID boş olamaz!");
            return;
        }

        if (ulong.TryParse(lobbyIdString, out ulong lobbyIDasULong))
        {
            CSteamID lobbySteamID = new CSteamID(lobbyIDasULong);
            Debug.Log($"MainMenuManager: Lobiye {lobbySteamID} ID ile katılınıyor...");
            SteamMatchmaking.JoinLobby(lobbySteamID);
        }
        else
        {
            Debug.LogError("MainMenuManager: Geçersiz Lobi ID formatı. Sadece rakamlar içermeli.");
        }
    }

    public void OnClick_BackFromLobbyJoin() // Lobiye katıl panelinden geri butonu
    {
        if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false); // Diğer panelleri kapat
    }

    public void OnClick_BackFromSettings() // Yeni eklendi: Ayarlar panelinden geri butonu
    {
        Debug.Log("MainMenuManager: Ayarlar ekranından geri dönülüyor.");
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(false); // Diğer panelleri kapat
    }

    // --- Steamworks.NET Callback Fonksiyonları ---

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult == EResult.k_EResultOK)
        {
            Debug.Log($"MainMenuManager: Steam Lobisi başarıyla oluşturuldu! ID: {result.m_ulSteamIDLobby}");

            LobbyManager.staticLobbyID = new CSteamID(result.m_ulSteamIDLobby);

            if (steamworksTransportInstance != null)
            {
                steamworksTransportInstance.StartConnection(true);
            }
            else
            {
                Debug.LogError("MainMenuManager: FishySteamworks Transport referansı boş! Sunucu başlatılamadı.");
                return;
            }

            // ✅ FishNet sunucu başlatılmalı (Eksik olan buydu!)
            if (!networkManager.IsServerStarted)
            {
                Debug.Log("MainMenuManager: FishNet sunucusu başlatılıyor...");
                networkManager.ServerManager.StartConnection();
            }

            // ✅ Lobi sahnesini yükle
            SceneLoadData sldLobbyCreated = new SceneLoadData(lobbySceneName);
            sldLobbyCreated.ReplaceScenes = ReplaceOption.All;
            networkManager.SceneManager.LoadGlobalScenes(sldLobbyCreated);
        }
        else
        {
            Debug.LogError($"MainMenuManager: Steam Lobisi oluşturulamadı: {result.m_eResult}");
            networkManager.ServerManager.StopConnection(true);
            steamworksTransportInstance.StopConnection(true);
        }
    }


    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        Debug.Log($"MainMenuManager: Lobiye katılma isteği alındı. Lobi ID: {result.m_steamIDLobby}");
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            Debug.Log($"MainMenuManager: Steam Lobisine başarıyla katıldı! Lobi ID: {result.m_ulSteamIDLobby}");

            LobbyManager.staticLobbyID = new CSteamID(result.m_ulSteamIDLobby);

            if (!networkManager.IsServerStarted)
            {
                Debug.Log("MainMenuManager: FishNet client başlatılıyor...");
                if (steamworksTransportInstance != null)
                {
                    steamworksTransportInstance.StartConnection(false);
                }
                else
                {
                    Debug.LogError("MainMenuManager: FishySteamworks Transport referansı boş! Client başlatılamadı.");
                    return;
                }
            }

            SceneLoadData sldLobbyEntered = new SceneLoadData(lobbySceneName);
            sldLobbyEntered.ReplaceScenes = ReplaceOption.All;
            networkManager.SceneManager.LoadGlobalScenes(sldLobbyEntered);
        }
        else
        {
            Debug.LogError($"MainMenuManager: Steam Lobisine katılamadı: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            networkManager.ClientManager.StopConnection();
            steamworksTransportInstance.StopConnection(true);

            if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
            if (lobbyJoinPanel != null) lobbyJoinPanel.SetActive(false);
            if (settingsPanel != null) settingsPanel.SetActive(false); // Diğer panelleri kapat
        }
    }
}
