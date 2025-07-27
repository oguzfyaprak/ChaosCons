using UnityEngine;
using UnityEngine.SceneManagement; // FishNet dışı sahne yüklemeleri için
using TMPro;
using FishNet.Managing;
using FishNet.Transporting;
using FishySteamworks;
using Steamworks;
using FishNet.Managing.Scened;
using UnityEngine.UI; // Button bileşeni için

public class MainMenuManager : MonoBehaviour
{
    // --- Referanslar ---
    public NetworkManager networkManager;
    private FishySteamworks.FishySteamworks steamworksTransportInstance;

    // UI Panelleri
    [SerializeField] private GameObject mainMenuPanel;          // Ana Menü
    [SerializeField] private GameObject lobbySelectionPanel;    // Lobi Oluştur/Katıl Seçim Paneli
    [SerializeField] private GameObject joinLobbyPanel;         // Lobi ID ile Katıl Paneli
    [SerializeField] private GameObject lobbyPanel;             // Lobi Ekranı Paneli
    [SerializeField] private GameObject settingsPanel;          // Ayarlar Paneli
    // [SerializeField] private GameObject aboutPanel;          // Hakkımızda Paneli (Ekleyebilirsiniz)

    // Lobi ID ile Katıl Paneli Elemanları
    [SerializeField] private TMP_InputField lobbyIdInputField;

    // Lobi Paneli Elemanları (LobbyManager tarafından erişilecek)
    // Bu elemanlar MainMenuManager'da SerializeField olarak tanımlı olmalı ki Inspector'dan atayabilesiniz.
    // LobbyManager bu referansları Initialize metoduyla MainMenuManager'dan alacak.
    [SerializeField] private TMP_Text lobbyIdText;
    [SerializeField] private TMP_Text playerListText;
    [SerializeField] private GameObject startGameButton;
    [SerializeField] private GameObject leaveLobbyButton;
    [SerializeField] private GameObject readyButton;

    // Yüklenecek ana oyun sahnesinin adı (Unity Build Settings'te ekli olmalı)
    [SerializeField] private string mainGameSceneName = "MainMap";
    // Ana menü sahnesinin adı (genellikle mevcut sahneniz, sahne geçişi için kullanışlı)
    [SerializeField] private string mainMenuSceneName = "MainMenuScene";


    // Steam API'den gelen Callback'ler için tutucular
    // Tüm Steamworks callback'leri burada merkezileştirildi.
    protected Callback<LobbyCreated_t> LobbyCreated;
    protected Callback<GameLobbyJoinRequested_t> GameLobbyJoinRequested;
    protected Callback<LobbyEnter_t> LobbyEntered;
    protected Callback<LobbyChatUpdate_t> LobbyChatUpdate;
    protected Callback<LobbyDataUpdate_t> LobbyDataUpdate;
    protected Callback<LobbyGameCreated_t> LobbyGameCreated;
    protected Callback<PersonaStateChange_t> PersonaStateChange;

    // Lobi Bilgisi (Bu sınıfın yönettiği lobi ID'si)
    private CSteamID _currentLobbyID;
    // Diğer sınıfların (örneğin LobbyManager) mevcut lobi ID'sine erişimi için statik değişken.
    public static CSteamID staticLobbyID;

    private LobbyManager _lobbyManagerInstance; // Lobi mantığını yönetecek LobbyManager referansı

    void Awake()
    {
        Debug.Log("MainMenuManager Awake çalıştı."); // Debug Log eklendi

        // NetworkManager ve Transport referanslarını al
        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
            if (networkManager == null)
            {
                Debug.LogError("MainMenuManager: NetworkManager bulunamadı! Lütfen sahnede bir NetworkManager olduğundan emin olun.");
                return;
            }
        }

        steamworksTransportInstance = networkManager.TransportManager.GetTransport<FishySteamworks.FishySteamworks>();
        if (steamworksTransportInstance == null)
        {
            Debug.LogError("MainMenuManager: FishySteamworks Transport bulunamadı! NetworkManager ile aynı GameObject üzerinde FishySteamworks bileşeni olduğundan emin olun.");
            return;
        }

        // Panelleri başlangıç durumuna ayarla (Sadece Ana Menü açık olacak)
        ShowPanel(mainMenuPanel);
        // Bu kısım zaten ShowPanel içinde hallediliyor, dolayısıyla gereksiz
        // lobbySelectionPanel.SetActive(false);
        // joinLobbyPanel.SetActive(false);
        // lobbyPanel.SetActive(false);
        // settingsPanel.SetActive(false);
        // if (aboutPanel != null) aboutPanel.SetActive(false);


        // Steamworks.NET başlatıldıysa callback'leri oluştur ve kaydol
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

        // LobbyManager instance'ını al (bu script ile aynı GameObject üzerinde olmalı)
        _lobbyManagerInstance = GetComponent<LobbyManager>();
        if (_lobbyManagerInstance == null)
        {
            Debug.LogError("MainMenuManager: LobbyManager bulunamadı! Lütfen bu GameObject'e bir LobbyManager bileşeni ekleyin.");
        }
        else
        {
            // LobbyManager'a gerekli UI elemanlarını ve referansları pasla
            _lobbyManagerInstance.Initialize(networkManager, steamworksTransportInstance,
                                             lobbyIdText, playerListText, startGameButton,
                                             leaveLobbyButton, readyButton, mainGameSceneName);
        }
    }

    // Ortak panel gösterme fonksiyonu: Sadece belirtilen paneli açar, diğerlerini kapatır.
    private void ShowPanel(GameObject panelToShow)
    {
        // Debug Log'lar eklendi
        Debug.Log($"ShowPanel çağrıldı. Gösterilecek panel: {(panelToShow != null ? panelToShow.name : "NULL")}");

        // Tüm panelleri önce kapat
        mainMenuPanel.SetActive(false);
        lobbySelectionPanel.SetActive(false);
        joinLobbyPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        settingsPanel.SetActive(false);
        // if (aboutPanel != null) aboutPanel.SetActive(false);

        // Sadece istenen paneli aç
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
        }

        // Kontrol amaçlı loglar
        Debug.Log($"MainMenuPanel durumu: {(mainMenuPanel != null ? mainMenuPanel.activeSelf.ToString() : "NULL")}");
        Debug.Log($"LobbySelectionPanel durumu: {(lobbySelectionPanel != null ? lobbySelectionPanel.activeSelf.ToString() : "NULL")}");
        Debug.Log($"JoinLobbyPanel durumu: {(joinLobbyPanel != null ? joinLobbyPanel.activeSelf.ToString() : "NULL")}");
        Debug.Log($"LobbyPanel durumu: {(lobbyPanel != null ? lobbyPanel.activeSelf.ToString() : "NULL")}");
        Debug.Log($"SettingsPanel durumu: {(settingsPanel != null ? settingsPanel.activeSelf.ToString() : "NULL")}");
    }

    // --- Ana Menü Buton Fonksiyonları ---

    public void OnClick_PlayGame()
    {
        Debug.Log("MainMenuManager: Play Game butonuna tıklandı! Lobi Seçim ekranı açılıyor.");
        ShowPanel(lobbySelectionPanel);
    }

    public void OnClick_Ayarlar()
    {
        Debug.Log("MainMenuManager: Ayarlar ekranı açılıyor.");
        ShowPanel(settingsPanel);
    }

    public void OnClick_Hakkimizda()
    {
        Debug.Log("MainMenuManager: Hakkımızda açıldı.");
        // ShowPanel(aboutPanel); // Eğer bir Hakkımızda paneliniz varsa
    }

    public void OnClick_OyundanCik()
    {
        Debug.Log("MainMenuManager: Oyundan çıkılıyor...");
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Editor'de çıkış için
#endif
    }

    public void OnClick_Feedback()
    {
        Application.OpenURL("https://www.orneksite.com/geribildirim"); // Örnek site
    }

    // --- Lobi Seçim Paneli Fonksiyonları ---

    public void OnClick_CreateLobby()
    {
        Debug.Log("MainMenuManager: Lobi oluşturuluyor...");
        if (steamworksTransportInstance != null)
        {
            // ELobbyType.k_ELobbyTypeFriendsOnly: Sadece arkadaşların görebileceği bir lobi
            // 4: Maksimum oyuncu sayısı
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
        }
        else
        {
            Debug.LogError("MainMenuManager: FishySteamworks Transport referansı boş! Lobi oluşturulamadı.");
        }
    }

    public void OnClick_JoinLobbyUI()
    {
        Debug.Log("MainMenuManager: Lobiye Katıl ID ekranı açılıyor.");
        ShowPanel(joinLobbyPanel);
    }

    // --- Lobiye Katıl Paneli Fonksiyonları ---

    public void OnClick_JoinLobbyById()
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

    // --- Geri Buton Fonksiyonları ---

    public void OnClick_BackFromLobbySelection()
    {
        Debug.Log("MainMenuManager: Lobi Seçiminden Ana Menüye dönülüyor.");
        ShowPanel(mainMenuPanel);
    }

    public void OnClick_BackFromJoinLobby()
    {
        Debug.Log("MainMenuManager: Lobiye Katıl'dan Lobi Seçimine dönülüyor.");
        ShowPanel(lobbySelectionPanel);
    }

    public void OnClick_BackFromSettings()
    {
        Debug.Log("MainMenuManager: Ayarlar ekranından geri dönülüyor.");
        ShowPanel(mainMenuPanel);
    }

    public void OnClick_LeaveCurrentLobby()
    {
        Debug.Log("MainMenuManager: Lobiden ayrılma isteği gönderildi.");
        // LobbyManager'ın lobiden ayrılma işlevini çağır
        _lobbyManagerInstance.OnClick_LeaveLobby();

        // Lobiden ayrılınca Ana Menü paneline dön
        // Bu panel geçişinin hemen gerçekleştiğinden emin olmalıyız.
        ShowPanel(mainMenuPanel);
        Debug.Log("MainMenuManager: Lobiden ayrılış sonrası ana menü gösterildi.");
    }

    // --- Steamworks.NET Callback Fonksiyonları ---
    // Bu metodlar Steamworks API'den gelen olaylara yanıt verir.

    private void OnLobbyCreated(LobbyCreated_t result)
    {
        if (result.m_eResult == EResult.k_EResultOK)
        {
            _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
            staticLobbyID = _currentLobbyID;

            Debug.Log($"✅ Steam lobisi başarıyla oluşturuldu! ID: {_currentLobbyID}");

            // Server bağlantısı (host için)
            if (!networkManager.IsServerStarted)
            {
                Debug.Log("🟢 Server başlatılıyor (Host)...");
                steamworksTransportInstance.StartConnection(true);
            }

            // Client bağlantısı (host kendi client'ı için)
            if (!networkManager.IsClientStarted)
            {
                Debug.Log("🟢 Client başlatılıyor (Host)...");
                steamworksTransportInstance.StartConnection(false);
            }

            // Lobi UI göster
            ShowPanel(lobbyPanel);

            // LobbyManager'a bilgi gönder
            _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
            _lobbyManagerInstance.UpdatePlayerList();

            Debug.Log("🎮 Host olarak oyun bağlantısı başarıyla kuruldu.");
        }
        else
        {
            Debug.LogError($"❌ Steam Lobisi oluşturulamadı: {result.m_eResult}");
            HandleConnectionFailure();
            ShowPanel(mainMenuPanel);
        }
    }


    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t result)
    {
        Debug.Log($"MainMenuManager: Lobiye katılma isteği alındı. Lobi ID: {result.m_steamIDLobby}");
        // Başka bir oyuncunun daveti üzerine veya Steam üzerinden lobiye katılma isteği geldiğinde
        SteamMatchmaking.JoinLobby(result.m_steamIDLobby);
    }

    private void OnLobbyEntered(LobbyEnter_t result)
    {
        if ((EChatRoomEnterResponse)result.m_EChatRoomEnterResponse == EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            _currentLobbyID = new CSteamID(result.m_ulSteamIDLobby);
            staticLobbyID = _currentLobbyID; // Statik ID'yi güncelle

            Debug.Log($"MainMenuManager: Steam Lobisine başarıyla katıldı! Lobi ID: {_currentLobbyID}");

            // Host olarak da, Client olarak da istemciyi başlatmalıyız.
            // Sadece eğer istemci henüz başlatılmamışsa bu işlemi yap.
            if (steamworksTransportInstance != null && !networkManager.IsClientStarted)
            {
                Debug.Log("MainMenuManager: FishNet istemcisi başlatılıyor...");
                steamworksTransportInstance.StartConnection(false); // false = client
            }
            else if (steamworksTransportInstance == null)
            {
                Debug.LogError("MainMenuManager: FishySteamworks Transport referansı boş! İstemci başlatılamadı.");
                HandleConnectionFailure();
                return;
            }
            else // networkManager.IsClientStarted zaten true ise (nadiren olabilir ama loglayalım)
            {
                Debug.Log("MainMenuManager: İstemci zaten başlatılmış durumda. Tekrar başlatmaya gerek yok.");
            }

            // Lobi UI'ını göster ve başlat
            ShowPanel(lobbyPanel);
            _lobbyManagerInstance.InitializeLobbyUI(_currentLobbyID);
            _lobbyManagerInstance.UpdatePlayerList(); // Oyuncu listesini ilk kez güncelle
            // CS1501 Hatası Düzeltildi: SetLobbyMemberData 3 argüman alır.
            SteamMatchmaking.SetLobbyMemberData(_currentLobbyID, "ReadyStatus", "false"); // Hazır durumunu false yap
            _lobbyManagerInstance.UpdateReadyButtonState(); // Hazır butonu durumunu güncelle

            // Lobby'deki diğer oyuncuların bilgilerini talep et (Adlarını göstermek için)
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
                SteamFriends.RequestUserInformation(memberSteamID, false);
            }
        }
        else
        {
            Debug.LogError($"MainMenuManager: Steam Lobisine katılamadı: {(EChatRoomEnterResponse)result.m_EChatRoomEnterResponse}");
            HandleConnectionFailure(); // Bağlantı hatası durumunda temizlik yap
            ShowPanel(mainMenuPanel); // Hata durumunda ana menüye dön
        }
    }

    private void OnLobbyChatUpdate(LobbyChatUpdate_t pCallback)
    {
        // Lobideki sohbet veya üye durumu güncellemeleri geldiğinde
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"MainMenuManager: Lobi Sohbet Güncellemesi: {pCallback.m_ulSteamIDUserChanged} için {pCallback.m_rgfChatMemberStateChange}");
            _lobbyManagerInstance.UpdatePlayerList(); // Oyuncu listesini yeniden çiz
        }
    }

    private void OnLobbyDataUpdate(LobbyDataUpdate_t pCallback)
    {
        // Lobi verileri güncellendiğinde (örneğin "GameStarted" verisi)
        if (pCallback.m_ulSteamIDLobby == _currentLobbyID.m_SteamID)
        {
            Debug.Log($"MainMenuManager: Lobi Veri Güncellemesi: {pCallback.m_ulSteamIDLobby}");
            _lobbyManagerInstance.UpdatePlayerList(); // Oyuncu listesini yeniden çiz

            // Eğer lobi host'u oyunu başlattıysa (ve biz host değilsek)
            if (!networkManager.IsServerStarted && SteamMatchmaking.GetLobbyData(_currentLobbyID, "GameStarted") == "true")
            {
                Debug.Log("MainMenuManager: Lobi host'u oyunu başlattı. Ana oyun sahnesine geçiliyor.");
                LoadMainGameScene(); // Ana oyun sahnesini yükle
            }
        }
    }

    private void OnLobbyGameCreated(LobbyGameCreated_t result)
    {
        // Bu callback, oyunun başlatıldığını ve sunucu detaylarının lobiye yazıldığını belirtir.
        // Client'lar LobbyDataUpdate callback'i ile "GameStarted" verisini kontrol ederek sahne yükler.
        Debug.Log($"MainMenuManager: Lobi için oyun oluşturuldu. Sunucu IP: {result.m_unIP}, Port: {result.m_usPort}, Lobi ID: {result.m_ulSteamIDLobby}");
    }

    private void OnPersonaStateChange(PersonaStateChange_t pCallback)
    {
        // Bir arkadaşın durumu değiştiğinde (örn: çevrimiçi/çevrimdışı, oyun adı değişimi)
        Debug.Log($"MainMenuManager: Persona durumu değişti: {pCallback.m_ulSteamID} için {pCallback.m_nChangeFlags}");
        if (_currentLobbyID.IsValid())
        {
            // Lobideki oyuncu listesini yenilemek için
            int numMembers = SteamMatchmaking.GetNumLobbyMembers(_currentLobbyID);
            for (int i = 0; i < numMembers; i++)
            {
                CSteamID memberSteamID = SteamMatchmaking.GetLobbyMemberByIndex(_currentLobbyID, i);
                if (memberSteamID.m_SteamID == pCallback.m_ulSteamID)
                {
                    _lobbyManagerInstance.UpdatePlayerList();
                    break;
                }
            }
        }
    }

    // Ortak bağlantı kesme/hata yönetimi metodu
    private void HandleConnectionFailure()
    {
        Debug.Log("MainMenuManager: Bağlantı hatası/kesilmesi durumu işleniyor...");
        if (networkManager.IsServerStarted)
        {
            networkManager.ServerManager.StopConnection(true); // Sunucuyu durdur
            Debug.Log("MainMenuManager: Sunucu durduruldu.");
        }
        if (networkManager.IsClientStarted)
        {
            networkManager.ClientManager.StopConnection(); // İstemciyi durdur
            Debug.Log("MainMenuManager: İstemci durduruldu.");
        }
        // FishNet'in StopConnection'ları genellikle transport'u da halleder.
        staticLobbyID = CSteamID.Nil; // Statik Lobi ID'sini sıfırla
        Debug.Log("MainMenuManager: Statik Lobi ID'si sıfırlandı.");
    }

    // Ana oyun sahnesini yükleme metodu
    public void LoadMainGameScene()
    {
        Debug.Log("MainMenuManager: Ana oyun sahnesi yükleniyor...");
        SceneLoadData sld = new SceneLoadData(mainGameSceneName);
        sld.ReplaceScenes = ReplaceOption.All; // Mevcut tüm sahneleri yeni sahne ile değiştir
        networkManager.SceneManager.LoadGlobalScenes(sld);
    }
}